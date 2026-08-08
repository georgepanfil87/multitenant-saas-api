namespace MultiTenantSaaS.Application.Abstractions;

/// <summary>
/// Tenantul asociat operațiunii curente. Este sursa unică de adevăr pentru izolarea datelor:
/// query filter-ul din DbContext și ștampilarea <c>TenantId</c> la salvare citesc de aici.
/// </summary>
/// <remarks>
/// Declarată în Application, implementată în Infrastructure. Astfel testele pot injecta
/// un tenant fix fără să pornească un server HTTP.
/// </remarks>
public interface ITenantContext
{
    /// <summary>
    /// Tenantul curent, sau <c>null</c> dacă nu s-a rezolvat niciunul (request public,
    /// job de background, migrare). <c>null</c> înseamnă „niciun rând vizibil", nu „toate".
    /// </summary>
    Guid? TenantId { get; }

    /// <summary>Slug-ul tenantului curent, pentru logare și mesaje de eroare.</summary>
    string? TenantSlug { get; }

    /// <summary>Dacă există un tenant rezolvat pentru operațiunea curentă.</summary>
    bool IsResolved { get; }

    /// <summary>
    /// Stabilește tenantul curent până la eliberarea obiectului returnat. Singura cale de
    /// mutare a contextului: middleware-ul de rezoluție (Pas 4) și onboarding-ul (Pas 6)
    /// o folosesc, restul aplicației doar citește.
    /// </summary>
    IDisposable BeginScope(Guid tenantId, string? tenantSlug = null);
}
