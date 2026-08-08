using MultiTenantSaaS.Application.Abstractions;

namespace MultiTenantSaaS.Infrastructure.MultiTenancy;

/// <summary>
/// Context de tenant gol, folosit acolo unde nu există request: unelte de design-time
/// (<c>dotnet ef</c>) și migrări.
/// </summary>
/// <remarks>
/// Cu acest context, query filter-ul compară cu <see cref="Guid.Empty"/> și nu returnează
/// niciun rând, iar orice scriere a unei entități tenant-scoped eșuează explicit.
/// Comportamentul dorit: absența unui tenant nu deschide accesul, îl închide.
/// </remarks>
public sealed class NullTenantContext : ITenantContext
{
    public Guid? TenantId => null;

    public string? TenantSlug => null;

    public bool IsResolved => false;

    public IDisposable BeginScope(Guid tenantId, string? tenantSlug = null) =>
        throw new NotSupportedException(
            "NullTenantContext este read-only. Folosește TenantContext pentru operațiuni cu tenant.");
}
