using MultiTenantSaaS.Application.Common;

namespace MultiTenantSaaS.Api.MultiTenancy;

/// <summary>
/// Citește tenantul din claim-ul <c>tenant_id</c> al JWT-ului. Sursa autoritară pentru
/// orice cerere autentificată.
/// </summary>
/// <remarks>
/// Depinde de <c>UseAuthentication()</c> să fi rulat deja: altfel <c>context.User</c> este
/// anonim și strategia returnează <c>null</c> în tăcere. De aceea ordinea din pipeline conteaza.
/// </remarks>
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
