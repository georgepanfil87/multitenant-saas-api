using MultiTenantSaaS.Application.Abstractions;

namespace MultiTenantSaaS.Infrastructure.MultiTenancy;

/// <summary>
/// Empty tenant context for code paths without a request: design-time tooling and migrations.
/// Reads match no row and writes fail explicitly, so the absence of a tenant closes access
/// rather than opening it.
/// </summary>
public sealed class NullTenantContext : ITenantContext
{
    public Guid? TenantId => null;

    public string? TenantSlug => null;

    public bool IsResolved => false;

    public IDisposable BeginScope(Guid tenantId, string? tenantSlug = null) =>
        throw new NotSupportedException(
            "NullTenantContext is read-only. Use TenantContext for tenant-scoped operations.");
}
