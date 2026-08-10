namespace MultiTenantSaaS.Domain.Common;

/// <summary>
/// Marks an entity as owned by a tenant. Implementing it is enough: the DbContext discovers
/// these types and applies the tenant query filter automatically.
/// </summary>
public interface ITenantEntity
{
    // Getter only, by design: the value is stamped exclusively by the DbContext on save,
    // so application code cannot assign the wrong tenant.
    Guid TenantId { get; }
}
