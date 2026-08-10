namespace MultiTenantSaaS.Domain.Common;

/// <summary>Base class for entities with an identity and timestamps.</summary>
public abstract class BaseEntity
{
    // Generated in code, not by the database, so the id is known before SaveChanges.
    public Guid Id { get; protected set; } = Guid.NewGuid();

    public DateTime CreatedAtUtc { get; protected set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; protected set; }

    protected void MarkAsUpdated() => UpdatedAtUtc = DateTime.UtcNow;
}
