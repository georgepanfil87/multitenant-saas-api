namespace MultiTenantSaaS.Domain.Common;

/// <summary>Clasă de bază pentru entitățile cu identitate proprie și audit temporal.</summary>
public abstract class BaseEntity
{
    /// <summary>Cheia primară. Generată în cod, ca ID-ul să fie cunoscut înainte de SaveChanges.</summary>
    public Guid Id { get; protected set; } = Guid.NewGuid();

    /// <summary>Momentul creării, în UTC.</summary>
    public DateTime CreatedAtUtc { get; protected set; } = DateTime.UtcNow;

    /// <summary>Momentul ultimei modificări, în UTC. <c>null</c> dacă entitatea n-a fost modificată.</summary>
    public DateTime? UpdatedAtUtc { get; protected set; }

    /// <summary>Marchează entitatea ca modificată.</summary>
    protected void MarkAsUpdated() => UpdatedAtUtc = DateTime.UtcNow;
}
