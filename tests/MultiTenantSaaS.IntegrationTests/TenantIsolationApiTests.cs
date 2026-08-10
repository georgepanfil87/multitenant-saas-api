using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace MultiTenantSaaS.IntegrationTests;

/// <summary>
/// Cross-organization isolation, verified over HTTP against a real PostgreSQL. Each test here
/// maps to a concrete way multi-tenant systems leak data in production.
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

        // Beta only has the "GEN" project created during onboarding, nothing from Alfa.
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

        // 404, not 403: a 403 would confirm the id exists somewhere on the platform.
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
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);   // other organization: allowed
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode); // same one: rejected
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

        // Beta's welcome ticket is untouched: the cascade stops at the deleted project.
        Assert.Equal(1, (await beta.GetJsonAsync("/api/tickets")).GetProperty("totalCount").GetInt32());
    }
}

/// <summary>
/// All test classes share one API instance and one PostgreSQL container: starting it takes a
/// few seconds, and isolation between tests comes from each creating its own organization.
/// </summary>
[CollectionDefinition(nameof(ApiCollection))]
#pragma warning disable CA1711 // The "Collection" suffix is required by xUnit's convention.
public sealed class ApiCollection : ICollectionFixture<ApiFactory>;
#pragma warning restore CA1711
