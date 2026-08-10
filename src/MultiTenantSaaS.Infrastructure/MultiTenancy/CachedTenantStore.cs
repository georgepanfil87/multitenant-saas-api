using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MultiTenantSaaS.Application.Abstractions;
using MultiTenantSaaS.Application.Common;
using MultiTenantSaaS.Infrastructure.Persistence;

namespace MultiTenantSaaS.Infrastructure.MultiTenancy;

/// <summary>
/// Caută tenanți în baza de date, cu memorare în cache.
/// </summary>
/// <remarks>
/// Cache-ul nu e optimizare prematură: rezoluția rulează la <b>fiecare</b> request, deci fără
/// el am adăuga un query în plus pe toată aplicația. Efectul secundar important este că
/// protejează și împotriva enumerării de slug-uri, pentru că memorăm și rezultatele negative.
/// </remarks>
public sealed class CachedTenantStore(ApplicationDbContext db, IMemoryCache cache) : ITenantStore
{
    private static readonly TimeSpan HitTtl = TimeSpan.FromMinutes(5);

    // Negativele expiră mai repede: un tenant nou creat trebuie să devină accesibil imediat
    // după onboarding, nu peste cinci minute.
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

        // Tenants nu implementează ITenantEntity, deci tabela nu e filtrată - exact ce
        // ne trebuie aici, fiindcă încă nu știm cine e tenantul curent.
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

        // Tenantul e cache-uit sub ambele forme de identificator, deci le eliminăm pe ambele.
        cache.Remove($"tenant:{tenant.Slug.ToLowerInvariant()}");
        cache.Remove($"tenant:{tenant.Id.ToString().ToLowerInvariant()}");
    }
}
