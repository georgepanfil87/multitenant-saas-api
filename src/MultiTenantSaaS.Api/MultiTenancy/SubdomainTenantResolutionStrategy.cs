using Microsoft.Extensions.Options;

namespace MultiTenantSaaS.Api.MultiTenancy;

/// <summary>Extracts the tenant slug from the subdomain: acme.api.example.com to "acme".</summary>
public sealed class SubdomainTenantResolutionStrategy(IOptions<TenantResolutionOptions> options)
    : ITenantResolutionStrategy
{
    public TenantIdentifierSource Source => TenantIdentifierSource.Subdomain;

    public string? TryResolve(HttpContext context)
    {
        var settings = options.Value;

        if (!settings.EnableSubdomainStrategy || string.IsNullOrWhiteSpace(settings.BaseDomain))
        {
            return null;
        }

        var host = context.Request.Host.Host;

        // Matched against the configured base domain rather than just taking the first label:
        // otherwise "api.example.com" would read as tenant "api", and since Host is a
        // client-supplied header an attacker could pick any slug.
        var suffix = "." + settings.BaseDomain;
        if (!host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var slug = host[..^suffix.Length];

        // One subdomain level only: "a.b.example.com" is not a valid tenant.
        return slug.Length == 0 || slug.Contains('.', StringComparison.Ordinal) ? null : slug;
    }
}
