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

    /// <summary>
    /// Elimină din cache intrările pentru un tenant. Necesar imediat după onboarding:
    /// dacă cineva a interogat slug-ul înainte de a exista, rezultatul negativ e memorat,
    /// iar organizația nou creată ar părea inexistentă până la expirarea lui.
    /// </summary>
    void Invalidate(TenantInfo tenant);
}
