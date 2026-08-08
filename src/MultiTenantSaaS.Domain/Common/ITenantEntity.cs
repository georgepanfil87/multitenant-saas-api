namespace MultiTenantSaaS.Domain.Common;

/// <summary>
/// Marchează o entitate ca aparținând unui tenant. Entitățile care implementează această
/// interfață primesc automat global query filter pe <c>TenantId</c> (vezi ApplicationDbContext).
/// </summary>
public interface ITenantEntity
{
    /// <summary>
    /// Tenantul proprietar. Expus doar cu getter, deliberat: valoarea e ștampilată exclusiv
    /// de DbContext la SaveChanges, ca să nu existe cale prin care codul de aplicație
    /// să atribuie un tenant greșit.
    /// </summary>
    Guid TenantId { get; }
}
