using MultiTenantSaaS.Application.Common;

namespace MultiTenantSaaS.Application.Abstractions;

/// <summary>Caută tenanți după identificatorul extras din request.</summary>
public interface ITenantStore
{
    /// <summary>
    /// Găsește un tenant după slug sau după ID (acceptă ambele forme, pentru că tokenul
    /// poartă ID, iar headerul și subdomeniul poartă slug).
    /// </summary>
    /// <returns><c>null</c> dacă identificatorul nu corespunde niciunui tenant.</returns>
    Task<TenantInfo?> FindAsync(string identifier, CancellationToken cancellationToken = default);
}
