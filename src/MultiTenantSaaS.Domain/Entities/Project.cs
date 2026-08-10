using MultiTenantSaaS.Domain.Common;

namespace MultiTenantSaaS.Domain.Entities;

/// <summary>A project inside an organization. Groups tickets.</summary>
public sealed class Project : BaseEntity, ITenantEntity
{
    private Project()
    {
        Name = string.Empty;
        Code = string.Empty;
    }

    public Guid TenantId { get; private set; }

    public string Name { get; private set; }

    /// <summary>Short code, unique within the tenant rather than globally.</summary>
    public string Code { get; private set; }

    public string? Description { get; private set; }

    /// <summary>When true, the project is read-only and hidden from default listings.</summary>
    public bool IsArchived { get; private set; }

    // Referenced by id only: Project and User are separate aggregates.
    public Guid CreatedByUserId { get; private set; }

    public static Project Create(string name, string code, Guid createdByUserId, string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        var normalizedCode = code.Trim().ToUpperInvariant();
        if (normalizedCode.Length is < 2 or > 10)
        {
            throw new ArgumentException("The project code must be 2 to 10 characters long.", nameof(code));
        }

        if (createdByUserId == Guid.Empty)
        {
            throw new ArgumentException("The project author is required.", nameof(createdByUserId));
        }

        return new Project
        {
            Name = name.Trim(),
            Code = normalizedCode,
            Description = description?.Trim(),
            CreatedByUserId = createdByUserId
        };
    }

    public void Update(string name, string? description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        Description = description?.Trim();
        MarkAsUpdated();
    }

    public void Archive()
    {
        IsArchived = true;
        MarkAsUpdated();
    }

    public void Restore()
    {
        IsArchived = false;
        MarkAsUpdated();
    }
}
