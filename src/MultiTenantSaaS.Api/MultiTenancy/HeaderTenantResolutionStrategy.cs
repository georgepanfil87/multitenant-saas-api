using Microsoft.Extensions.Options;

namespace MultiTenantSaaS.Api.MultiTenancy;

/// <summary>
/// Reads the tenant slug from an HTTP header (X-Tenant by default). The only strategy usable
/// before authentication, so it serves login. The value is client-supplied and carries no
/// authority: the middleware ignores it as soon as a token is present.
/// </summary>
public sealed class HeaderTenantResolutionStrategy(IOptions<TenantResolutionOptions> options)
    : ITenantResolutionStrategy
{
    public TenantIdentifierSource Source => TenantIdentifierSource.Header;

    public string? TryResolve(HttpContext context)
    {
        if (!options.Value.EnableHeaderStrategy)
        {
            return null;
        }

        return context.Request.Headers.TryGetValue(options.Value.HeaderName, out var values)
            ? values.FirstOrDefault()?.Trim()
            : null;
    }
}
