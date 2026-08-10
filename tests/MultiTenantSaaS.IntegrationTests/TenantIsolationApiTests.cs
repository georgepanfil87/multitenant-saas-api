using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace MultiTenantSaaS.IntegrationTests;

/// <summary>
/// Izolarea între organizații, verificată prin HTTP, peste PostgreSQL real.
/// Fiecare test din clasa asta corespunde unui mod concret în care un SaaS multi-tenant
/// scurge date în producție.
/// </summary>
[Collection(nameof(ApiCollection))]
public sealed class TenantIsolationApiTests(ApiFactory factory)
{
    [Fact]
    public async Task Tenant_SeesOnlyItsOwnProjects()
    {
        var (alfa, _, _) = await ApiClient.RegisterTenantAsync(factory, "iso-alfa");
        var (beta, _, _) = await ApiClient.RegisterTenantAsync(factory, "iso-beta");

        await alfa.PostJsonAsync("/api/projects", new { name = "Proiect Alfa", code = "ALF" });

        var betaProjects = await beta.GetJsonAsync("/api/projects");

        // Beta are doar proiectul „GEN" creat automat la onboarding, nimic de la Alfa.
        var codes = betaProjects.GetProperty("items").EnumerateArray()
            .Select(p => p.GetProperty("code").GetString()).ToList();

        Assert.Equal(["GEN"], codes);
        Assert.Equal(1, betaProjects.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Tenant_CannotReadAnotherTenantsProject_EvenWithExactId()
    {
        var (alfa, _, _) = await ApiClient.RegisterTenantAsync(factory, "read-alfa");
        var (beta, _, _) = await ApiClient.RegisterTenantAsync(factory, "read-beta");

        var project = await alfa.PostJsonAsync("/api/projects", new { name = "Secret", code = "SEC" });
        var id = project.GetProperty("id").GetString();

        var response = await beta.GetAsync($"/api/projects/{id}");

        // 404, nu 403: un 403 ar confirma că ID-ul există undeva în platformă.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Tenant_CannotCreateTicketInAnotherTenantsProject()
    {
        var (alfa, _, _) = await ApiClient.RegisterTenantAsync(factory, "tick-alfa");
        var (beta, _, _) = await ApiClient.RegisterTenantAsync(factory, "tick-beta");

        var project = await alfa.PostJsonAsync("/api/projects", new { name = "Alfa", code = "ALF" });

        var response = await beta.PostAsync("/api/tickets", new
        {
            projectId = project.GetProperty("id").GetString(),
            title = "Tichet strecurat"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Tenant_CannotAssignTicketToUserOfAnotherTenant()
    {
        var (alfa, _, _) = await ApiClient.RegisterTenantAsync(factory, "asg-alfa");
        var (_, _, betaAdmin) = await ApiClient.RegisterTenantAsync(factory, "asg-beta");

        var projects = await alfa.GetJsonAsync("/api/projects");
        var projectId = projects.GetProperty("items")[0].GetProperty("id").GetString();

        var ticket = await alfa.PostJsonAsync("/api/tickets", new { projectId, title = "De alocat" });

        var response = await alfa.PatchAsync(
            $"/api/tickets/{ticket.GetProperty("id").GetString()}/assignee",
            new { assignedToUserId = betaAdmin.GetProperty("id").GetString() });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Token_OfOneTenant_WithHeaderOfAnother_IsRejected()
    {
        var (alfa, _, _) = await ApiClient.RegisterTenantAsync(factory, "hdr-alfa");
        await ApiClient.RegisterTenantAsync(factory, "hdr-beta");

        alfa.Http.DefaultRequestHeaders.Add("X-Tenant", "hdr-beta");

        var response = await alfa.GetAsync("/api/projects");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithEmailThatExistsInAnotherTenant_Fails()
    {
        await ApiClient.RegisterTenantAsync(factory, "log-alfa");
        await ApiClient.RegisterTenantAsync(factory, "log-beta");

        var anonymous = factory.CreateClient();
        anonymous.DefaultRequestHeaders.Add("X-Tenant", "log-beta");

        var response = await anonymous.PostAsJsonAsync("/api/auth/login", new
        {
            email = "admin@log-alfa.ro",
            password = "Parola-Sigura-123"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProjectCode_IsUniquePerTenant_NotGlobally()
    {
        var (alfa, _, _) = await ApiClient.RegisterTenantAsync(factory, "cod-alfa");
        var (beta, _, _) = await ApiClient.RegisterTenantAsync(factory, "cod-beta");

        var first = await alfa.PostAsync("/api/projects", new { name = "Suport", code = "SUP" });
        var second = await beta.PostAsync("/api/projects", new { name = "Suport", code = "SUP" });
        var duplicate = await alfa.PostAsync("/api/projects", new { name = "Alt suport", code = "SUP" });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);   // altă organizație: permis
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode); // aceeași: respins
    }

    [Fact]
    public async Task UnauthenticatedRequest_IsRejected()
    {
        var response = await factory.CreateClient().GetAsync(new Uri("/api/projects", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ForgedToken_IsRejected()
    {
        var (alfa, _, _) = await ApiClient.RegisterTenantAsync(factory, "frg-alfa");

        var tampered = alfa.Http.DefaultRequestHeaders.Authorization!.Parameter + "modificat";
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tampered);

        var response = await client.GetAsync(new Uri("/api/projects", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeletingProject_CascadesTicketsWithinTenantOnly()
    {
        var (alfa, _, _) = await ApiClient.RegisterTenantAsync(factory, "del-alfa");
        var (beta, _, _) = await ApiClient.RegisterTenantAsync(factory, "del-beta");

        var alfaProjects = await alfa.GetJsonAsync("/api/projects");
        var alfaProjectId = alfaProjects.GetProperty("items")[0].GetProperty("id").GetString();

        var response = await alfa.DeleteAsync($"/api/projects/{alfaProjectId}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        Assert.Equal(0, (await alfa.GetJsonAsync("/api/tickets")).GetProperty("totalCount").GetInt32());

        // Tichetul de bun venit al lui Beta e neatins: cascada e limitată la proiectul șters.
        Assert.Equal(1, (await beta.GetJsonAsync("/api/tickets")).GetProperty("totalCount").GetInt32());
    }
}

/// <summary>
/// Toate clasele de teste partajează o singură instanță de API și un singur container
/// PostgreSQL: pornirea containerului durează câteva secunde, iar izolarea între teste
/// vine din faptul că fiecare își creează propria organizație.
/// </summary>
[CollectionDefinition(nameof(ApiCollection))]
#pragma warning disable CA1711 // Sufixul „Collection" e cerut de convenția xUnit.
public sealed class ApiCollection : ICollectionFixture<ApiFactory>;
#pragma warning restore CA1711
