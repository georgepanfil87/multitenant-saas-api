namespace MultiTenantSaaS.Application.Abstractions;

/// <summary>
/// The tenant of the current operation. Single source of truth for isolation: the query filter
/// and the TenantId stamping both read from here.
/// </summary>
public interface ITenantContext
{
    /// <summary>Current tenant, or null when none was resolved. Null means "no rows", not "all rows".</summary>
    Guid? TenantId { get; }

    string? TenantSlug { get; }

    bool IsResolved { get; }

    /// <summary>
    /// Sets the current tenant until the returned object is disposed. The only mutation path:
    /// used by the resolution middleware and by tenant onboarding.
    /// </summary>
    IDisposable BeginScope(Guid tenantId, string? tenantSlug = null);
}
