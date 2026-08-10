using MultiTenantSaaS.Application.Common;

namespace MultiTenantSaaS.Api.MultiTenancy;

/// <summary>
/// Reads the tenant from the JWT tenant_id claim. Authoritative for authenticated requests.
/// Requires UseAuthentication() to have run: otherwise the user is anonymous and this returns
/// null silently, which is why pipeline order matters.
/// </summary>
public sealed class ClaimTenantResolutionStrategy : ITenantResolutionStrategy
{
    public TenantIdentifierSource Source => TenantIdentifierSource.Token;

    public string? TryResolve(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var value = context.User.FindFirst(TenantClaimTypes.TenantId)?.Value;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
