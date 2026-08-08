using Microsoft.Extensions.Options;

namespace MultiTenantSaaS.Api.MultiTenancy;

/// <summary>
/// Citește slug-ul tenantului dintr-un header HTTP (implicit <c>X-Tenant</c>).
/// </summary>
/// <remarks>
/// Este singura strategie utilizabilă înainte de autentificare, deci deservește login-ul:
/// „vreau să mă loghez la organizația acme". Valoarea vine de la client, deci nu are nicio
/// autoritate proprie - middleware-ul o ignoră imediat ce există un token.
/// </remarks>
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
