using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace MultiTenantSaaS.IntegrationTests;

/// <summary>
/// Behaviour that cannot be verified on the in-memory provider or without the HTTP pipeline.
/// Each test here corresponds to a real bug found during development.
/// </summary>
[Collection(nameof(ApiCollection))]
public sealed class SqlBehaviourApiTests(ApiFactory factory)
{
    [Fact]
    public async Task TicketListing_TranslatesProjectionToSql()
    {
        // Regression: the projection was a static method call inside Select, which EF Core
        // cannot translate. In-memory evaluated it client-side with a null Project; PostgreSQL
        // would have thrown "could not be translated".
        var (client, _, _) = await ApiClient.RegisterTenantAsync(factory, "sql-proj");

        var tickets = await client.GetJsonAsync("/api/tickets");
        var first = tickets.GetProperty("items")[0];

        // projectCode comes from the join to Projects, proving the translation works.
        Assert.Equal("GEN", first.GetProperty("projectCode").GetString());
    }

    [Fact]
    public async Task Pagination_BindsFromQueryString()
    {
        // Regression: the action parameter was named "page", colliding with a query-string key,
        // so the model binder switched to the "page." prefix and ignored "pageSize". No unit test
        // could catch this: the bug was in binding, not in logic.
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
        Assert.Equal(5, firstPage.GetProperty("totalCount").GetInt32()); // 4 plus the welcome ticket
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

        // "%" must be treated as text, not a wildcard, or every ticket would match.
        var byPercent = await client.GetJsonAsync("/api/tickets?search=100%25");
        Assert.Single(byPercent.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task DueDateFromJson_IsStoredAsUtc()
    {
        // Npgsql rejects Unspecified DateTime values for timestamptz, and anything coming from
        // JSON is exactly that. The DbContext converter normalizes it.
        var (client, _, _) = await ApiClient.RegisterTenantAsync(factory, "sql-data");

        var projects = await client.GetJsonAsync("/api/projects");
        var projectId = projects.GetProperty("items")[0].GetProperty("id").GetString();

        var created = await client.PostJsonAsync("/api/tickets", new
        {
            projectId,
            title = "Cu termen limită",
            dueDateUtc = "2026-12-31T23:59:00"   // deliberately without a time zone
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
