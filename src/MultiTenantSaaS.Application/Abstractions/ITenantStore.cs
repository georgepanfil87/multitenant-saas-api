using MultiTenantSaaS.Application.Common;

namespace MultiTenantSaaS.Application.Abstractions;

/// <summary>Looks up tenants by the identifier extracted from a request.</summary>
public interface ITenantStore
{
    /// <summary>Finds a tenant by slug or by id: tokens carry the id, headers carry the slug.</summary>
    Task<TenantInfo?> FindAsync(string identifier, CancellationToken cancellationToken = default);

    /// <summary>
    /// Drops cached entries for a tenant. Required right after onboarding, otherwise a cached
    /// negative lookup would make the new organization look non-existent.
    /// </summary>
    void Invalidate(TenantInfo tenant);
}
