using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace MultiTenantSaaS.IntegrationTests;

/// <summary>Thin wrapper over <see cref="HttpClient"/> so tests read as scenarios, not plumbing.</summary>
public sealed class ApiClient(HttpClient http)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public HttpClient Http => http;

    /// <summary>Registers an organization and returns a client authenticated as its admin.</summary>
    public static async Task<(ApiClient Client, JsonElement Tenant, JsonElement Admin)> RegisterTenantAsync(
        ApiFactory factory, string slug)
    {
        ArgumentNullException.ThrowIfNull(factory);

        var anonymous = factory.CreateClient();
        var response = await anonymous.PostAsJsonAsync("/api/tenants/register", new
        {
            organizationName = $"Organizația {slug}",
            slug,
            adminEmail = $"admin@{slug}.ro",
            adminPassword = "Parola-Sigura-123",
            adminFullName = $"Admin {slug}"
        });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());

        return (new ApiClient(client), body.GetProperty("tenant"), body.GetProperty("admin"));
    }

    public async Task<JsonElement> GetJsonAsync(string url)
    {
        var response = await http.GetAsync(new Uri(url, UriKind.Relative));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    public Task<HttpResponseMessage> GetAsync(string url) => http.GetAsync(new Uri(url, UriKind.Relative));

    public Task<HttpResponseMessage> PostAsync<T>(string url, T body) =>
        http.PostAsJsonAsync(new Uri(url, UriKind.Relative), body, Json);

    public Task<HttpResponseMessage> PatchAsync<T>(string url, T body) =>
        http.PatchAsJsonAsync(new Uri(url, UriKind.Relative), body, Json);

    public Task<HttpResponseMessage> DeleteAsync(string url) => http.DeleteAsync(new Uri(url, UriKind.Relative));

    public async Task<JsonElement> PostJsonAsync<T>(string url, T body)
    {
        var response = await PostAsync(url, body);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}
