using MultiTenantSaaS.Domain.Common;

namespace MultiTenantSaaS.Domain.Entities;

/// <summary>Un proiect din interiorul unei organizații. Grupează tichete.</summary>
public sealed class Project : BaseEntity, ITenantEntity
{
    private Project()
    {
        Name = string.Empty;
        Code = string.Empty;
    }

    /// <summary>Organizația proprietară. Ștampilat automat la salvare.</summary>
    public Guid TenantId { get; private set; }

    /// <summary>Numele proiectului.</summary>
    public string Name { get; private set; }

    /// <summary>Cod scurt, unic în cadrul tenantului (nu global). Ex: <c>SUP</c>.</summary>
    public string Code { get; private set; }

    /// <summary>Descriere opțională.</summary>
    public string? Description { get; private set; }

    /// <summary>Dacă e <c>true</c>, proiectul e read-only și ascuns din listările implicite.</summary>
    public bool IsArchived { get; private set; }

    /// <summary>Utilizatorul care a creat proiectul. Referință prin ID, fără navigație între agregate.</summary>
    public Guid CreatedByUserId { get; private set; }

    /// <summary>Creează un proiect nou.</summary>
    public static Project Create(string name, string code, Guid createdByUserId, string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        var normalizedCode = code.Trim().ToUpperInvariant();
        if (normalizedCode.Length is < 2 or > 10)
        {
            throw new ArgumentException("Codul proiectului are între 2 și 10 caractere.", nameof(code));
        }

        if (createdByUserId == Guid.Empty)
        {
            throw new ArgumentException("Autorul proiectului este obligatoriu.", nameof(createdByUserId));
        }

        return new Project
        {
            Name = name.Trim(),
            Code = normalizedCode,
            Description = description?.Trim(),
            CreatedByUserId = createdByUserId
        };
    }

    /// <summary>Actualizează numele și descrierea.</summary>
    public void Update(string name, string? description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        Description = description?.Trim();
        MarkAsUpdated();
    }

    /// <summary>Arhivează proiectul.</summary>
    public void Archive()
    {
        IsArchived = true;
        MarkAsUpdated();
    }

    /// <summary>Scoate proiectul din arhivă.</summary>
    public void Restore()
    {
        IsArchived = false;
        MarkAsUpdated();
    }
}
