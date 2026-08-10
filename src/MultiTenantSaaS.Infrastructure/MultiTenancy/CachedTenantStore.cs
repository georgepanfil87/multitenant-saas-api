using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MultiTenantSaaS.Application.Abstractions;
using MultiTenantSaaS.Application.Common;
using MultiTenantSaaS.Infrastructure.Persistence;

namespace MultiTenantSaaS.Infrastructure.MultiTenancy;

/// <summary>
/// Database-backed tenant lookup with caching. Resolution runs on every request, so without a
/// cache this would add one query to the entire application. Negative results are cached too,
/// which also blunts slug enumeration.
/// </summary>
public sealed class CachedTenantStore(ApplicationDbContext db, IMemoryCache cache) : ITenantStore
{
    private static readonly TimeSpan HitTtl = TimeSpan.FromMinutes(5);

    // Misses expire sooner: a freshly onboarded tenant must be reachable right away.
    private static readonly TimeSpan MissTtl = TimeSpan.FromSeconds(30);

    public async Task<TenantInfo?> FindAsync(string identifier, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return null;
        }

        var key = $"tenant:{identifier.ToLowerInvariant()}";
        if (cache.TryGetValue<TenantInfo?>(key, out var cached))
        {
            return cached;
        }

        // Tenants is not an ITenantEntity, so the table is unfiltered, which is exactly what we
        // need here: the current tenant is not known yet.
        var query = db.Tenants.AsNoTracking();

        var normalized = identifier.ToLowerInvariant();
        var tenant = Guid.TryParse(identifier, out var id)
            ? await query.FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            : await query.FirstOrDefaultAsync(t => t.Slug == normalized, cancellationToken);

        var info = tenant is null
            ? null
            : new TenantInfo(tenant.Id, tenant.Slug, tenant.Name, tenant.Plan, tenant.IsActive,
                tenant.RequestsPerMinuteOverride);

        cache.Set(key, info, info is null ? MissTtl : HitTtl);
        return info;
    }

    public void Invalidate(TenantInfo tenant)
    {
        ArgumentNullException.ThrowIfNull(tenant);

        // The tenant is cached under both identifier forms, so drop both.
        cache.Remove($"tenant:{tenant.Slug.ToLowerInvariant()}");
        cache.Remove($"tenant:{tenant.Id.ToString().ToLowerInvariant()}");
    }
}
