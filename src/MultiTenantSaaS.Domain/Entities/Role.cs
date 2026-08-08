using MultiTenantSaaS.Domain.Enums;

namespace MultiTenantSaaS.Domain.Entities;

/// <summary>
/// Tabelă de lookup pentru roluri, cu exact trei rânduri seed-uite la migrare.
/// Enum-ul <see cref="UserRole"/> rămâne sursa de adevăr în cod; tabela există pentru
/// foreign key real în PostgreSQL și nume citibile la inspecția bazei.
/// </summary>
/// <remarks>
/// Rolurile sunt globale, nu per-tenant, deci entitatea nu implementează <c>ITenantEntity</c>.
/// </remarks>
public sealed class Role
{
    private Role()
    {
        Name = string.Empty;
        Description = string.Empty;
    }

    /// <summary>Cheia primară, identică cu valoarea din <see cref="UserRole"/>.</summary>
    public UserRole Id { get; private set; }

    /// <summary>Numele tehnic, folosit în claim-ul <c>role</c> din JWT și în policy-uri.</summary>
    public string Name { get; private set; }

    /// <summary>Descriere lizibilă.</summary>
    public string Description { get; private set; }

    /// <summary>Construiește un rând de lookup. Folosit la seed-ul din migrare.</summary>
    public static Role Create(UserRole id, string name, string description) => new()
    {
        Id = id,
        Name = name,
        Description = description
    };
}
