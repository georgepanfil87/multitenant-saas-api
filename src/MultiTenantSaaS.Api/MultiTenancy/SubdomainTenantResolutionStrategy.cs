using Microsoft.Extensions.Options;

namespace MultiTenantSaaS.Api.MultiTenancy;

/// <summary>
/// Extrage slug-ul tenantului din subdomeniu: <c>acme.api.exemplu.ro</c> → <c>acme</c>.
/// </summary>
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

        // Comparăm cu domeniul de bază configurat, nu luăm pur și simplu prima etichetă:
        // altfel "api.exemplu.ro" ar fi interpretat ca tenantul "api", iar un atacator
        // ar putea forța ce slug vrea printr-un header Host trimis de el.
        var suffix = "." + settings.BaseDomain;
        if (!host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var slug = host[..^suffix.Length];

        // Doar un nivel de subdomeniu: "a.b.exemplu.ro" nu e un tenant valid.
        return slug.Length == 0 || slug.Contains('.', StringComparison.Ordinal) ? null : slug;
    }
}
