namespace MultiTenantSaaS.Api.MultiTenancy;

/// <summary>Tenant resolution settings, bound from the "MultiTenancy" configuration section.</summary>
public sealed class TenantResolutionOptions
{
    public const string SectionName = "MultiTenancy";

    /// <summary>Header carrying the tenant slug.</summary>
    public string HeaderName { get; set; } = "X-Tenant";

    /// <summary>Whether the header strategy is enabled.</summary>
    public bool EnableHeaderStrategy { get; set; } = true;

    /// <summary>Whether the subdomain strategy is enabled.</summary>
    public bool EnableSubdomainStrategy { get; set; }

    /// <summary>
    /// Base domain the subdomain is extracted against. With api.example.com,
    /// acme.api.example.com yields the slug "acme".
    /// </summary>
    public string? BaseDomain { get; set; }

    /// <summary>Paths that need no tenant and must not fail because of one.</summary>
    public string[] SkipPaths { get; set; } = ["/health", "/swagger"];
}
