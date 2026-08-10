using MultiTenantSaaS.Domain.Enums;

namespace MultiTenantSaaS.Domain.Entities;

/// <summary>
/// Lookup table with exactly three rows, seeded by migration. The <see cref="UserRole"/> enum
/// is the source of truth in code; the table adds a real foreign key and readable names in SQL.
/// </summary>
public sealed class Role
{
    private Role()
    {
        Name = string.Empty;
        Description = string.Empty;
    }

    public UserRole Id { get; private set; }

    /// <summary>Technical name, used in the JWT role claim and in authorization policies.</summary>
    public string Name { get; private set; }

    public string Description { get; private set; }

    public static Role Create(UserRole id, string name, string description) => new()
    {
        Id = id,
        Name = name,
        Description = description
    };
}
