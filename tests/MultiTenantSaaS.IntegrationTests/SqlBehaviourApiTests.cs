using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace MultiTenantSaaS.IntegrationTests;

/// <summary>
/// Comportamente care nu se pot verifica pe provider-ul in-memory sau fără pipeline HTTP.
/// Fiecare test de aici corespunde unui bug real apărut în timpul dezvoltării.
/// </summary>
[Collection(nameof(ApiCollection))]
public sealed class SqlBehaviourApiTests(ApiFactory factory)
{
    [Fact]
    public async Task TicketListing_TranslatesProjectionToSql()
    {
        // Regresie: proiecția era un apel de metodă statică în Select, pe care EF Core
        // nu-l poate traduce. In-memory evalua pe client (cu Project null); pe PostgreSQL
        // ar fi aruncat „could not be translated".
        var (client, _, _) = await ApiClient.RegisterTenantAsync(factory, "sql-proj");

        var tickets = await client.GetJsonAsync("/api/tickets");
        var first = tickets.GetProperty("items")[0];

        // projectCode vine din JOIN-ul către Projects, deci dovedește traducerea corectă.
        Assert.Equal("GEN", first.GetProperty("projectCode").GetString());
    }

    [Fact]
    public async Task Pagination_BindsFromQueryString()
    {
        // Regresie: parametrul de acțiune se numea „page", la fel ca o cheie din query string,
        // iar model binder-ul comuta pe prefixul „page.” și ignora „pageSize”. Niciun test
        // unitar nu putea prinde asta - bug-ul era în binding, nu în logică.
        var (client, _, _) = await ApiClient.RegisterTenantAsync(factory, "sql-pag");

        var projects = await client.GetJsonAsync("/api/projects");
        var projectId = projects.GetProperty("items")[0].GetProperty("id").GetString();

        for (var i = 0; i < 4; i++)
        {
            await client.PostJsonAsync("/api/tickets", new { projectId, title = $"Tichet {i}" });
        }

        var firstPage = await client.GetJsonAsync("/api/tickets?page=1&pageSize=2");

        Assert.Equal(2, firstPage.GetProperty("pageSize").GetInt32());
        Assert.Equal(2, firstPage.GetProperty("items").GetArrayLength());
        Assert.Equal(5, firstPage.GetProperty("totalCount").GetInt32()); // 4 + tichetul de bun venit
        Assert.Equal(3, firstPage.GetProperty("totalPages").GetInt32());
        Assert.True(firstPage.GetProperty("hasNextPage").GetBoolean());

        var lastPage = await client.GetJsonAsync("/api/tickets?page=3&pageSize=2");
        Assert.Single(lastPage.GetProperty("items").EnumerateArray());
        Assert.False(lastPage.GetProperty("hasNextPage").GetBoolean());
    }

    [Fact]
    public async Task PageSize_AboveCap_IsRejected()
    {
        var (client, _, _) = await ApiClient.RegisterTenantAsync(factory, "sql-cap");

        var response = await client.GetAsync("/api/tickets?pageSize=100000");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Search_IsCaseInsensitive_AndEscapesWildcards()
    {
        var (client, _, _) = await ApiClient.RegisterTenantAsync(factory, "sql-cauta");

        var projects = await client.GetJsonAsync("/api/projects");
        var projectId = projects.GetProperty("items")[0].GetProperty("id").GetString();

        await client.PostJsonAsync("/api/tickets", new { projectId, title = "Eroare la LOGIN" });
        await client.PostJsonAsync("/api/tickets", new { projectId, title = "Discount 100% aplicat gresit" });

        var byLowercase = await client.GetJsonAsync("/api/tickets?search=login");
        Assert.Single(byLowercase.GetProperty("items").EnumerateArray());

        // „%" trebuie tratat ca text, nu ca wildcard: altfel ar returna toate tichetele.
        var byPercent = await client.GetJsonAsync("/api/tickets?search=100%25");
        Assert.Single(byPercent.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task DueDateFromJson_IsStoredAsUtc()
    {
        // Npgsql respinge un DateTime cu Kind.Unspecified pentru timestamptz, iar orice dată
        // venită din JSON este exact Unspecified. Convertorul din DbContext o normalizează.
        var (client, _, _) = await ApiClient.RegisterTenantAsync(factory, "sql-data");

        var projects = await client.GetJsonAsync("/api/projects");
        var projectId = projects.GetProperty("items")[0].GetProperty("id").GetString();

        var created = await client.PostJsonAsync("/api/tickets", new
        {
            projectId,
            title = "Cu termen limită",
            dueDateUtc = "2026-12-31T23:59:00"   // fără fus orar, deliberat
        });

        var stored = await client.GetJsonAsync($"/api/tickets/{created.GetProperty("id").GetString()}");

        Assert.Equal(
            new DateTime(2026, 12, 31, 23, 59, 0, DateTimeKind.Utc),
            stored.GetProperty("dueDateUtc").GetDateTime().ToUniversalTime());
    }

    [Fact]
    public async Task StatusTransitions_AreEnforcedOverHttp()
    {
        var (client, _, _) = await ApiClient.RegisterTenantAsync(factory, "sql-stari");

        var tickets = await client.GetJsonAsync("/api/tickets");
        var id = tickets.GetProperty("items")[0].GetProperty("id").GetString();

        var forbidden = await client.PatchAsync($"/api/tickets/{id}/status", new { status = 4 });
        Assert.Equal(HttpStatusCode.BadRequest, forbidden.StatusCode);

        foreach (var status in new[] { 2, 3, 4 })
        {
            var allowed = await client.PatchAsync($"/api/tickets/{id}/status", new { status });
            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        }

        var closed = await client.GetJsonAsync($"/api/tickets/{id}");
        Assert.NotEqual(JsonValueKind.Null, closed.GetProperty("closedAtUtc").ValueKind);
    }

    [Fact]
    public async Task MemberRole_CannotDeleteTickets()
    {
        var (admin, _, _) = await ApiClient.RegisterTenantAsync(factory, "sql-rol");

        await admin.PostJsonAsync("/api/users", new
        {
            email = "membru@sql-rol.ro",
            password = "Parola-Membru-1",
            fullName = "Membru Simplu",
            role = 3
        });

        var anonymous = factory.CreateClient();
        anonymous.DefaultRequestHeaders.Add("X-Tenant", "sql-rol");
        var login = await anonymous.PostAsJsonAsync(new Uri("/api/auth/login", UriKind.Relative), new
        {
            email = "membru@sql-rol.ro",
            password = "Parola-Membru-1"
        });

        var token = (await login.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessToken").GetString();

        var member = factory.CreateClient();
        member.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var tickets = await new ApiClient(member).GetJsonAsync("/api/tickets");
        var id = tickets.GetProperty("items")[0].GetProperty("id").GetString();

        var response = await member.DeleteAsync(new Uri($"/api/tickets/{id}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
