using MultiTenantSaaS.Domain.Common;
using MultiTenantSaaS.Domain.Enums;

namespace MultiTenantSaaS.Domain.Entities;

/// <summary>
/// Un cont de utilizator, care aparține exact unei organizații.
/// Unicitatea emailului este <c>(TenantId, Email)</c>, nu globală: aceeași persoană poate
/// avea cont la mai multe organizații client.
/// </summary>
public sealed class User : BaseEntity, ITenantEntity
{
    private User()
    {
        Email = string.Empty;
        PasswordHash = string.Empty;
        FullName = string.Empty;
    }

    /// <summary>Organizația căreia îi aparține contul. Ștampilat automat la salvare.</summary>
    public Guid TenantId { get; private set; }

    /// <summary>Emailul, folosit ca nume de utilizator la login.</summary>
    public string Email { get; private set; }

    /// <summary>Hash-ul parolei. Parola în clar nu atinge niciodată această entitate.</summary>
    public string PasswordHash { get; private set; }

    /// <summary>Numele complet, pentru afișare.</summary>
    public string FullName { get; private set; }

    /// <summary>Nivelul de acces. Este în același timp cheia străină către tabela <c>Roles</c>.</summary>
    public UserRole Role { get; private set; } = UserRole.Member;

    /// <summary>Dacă e <c>false</c>, login-ul este refuzat.</summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>Ultima autentificare reușită, în UTC.</summary>
    public DateTime? LastLoginAtUtc { get; private set; }

    /// <summary>Navigație către organizație.</summary>
    public Tenant? Tenant { get; private set; }

    /// <summary>Creează un cont nou. Apartenența la tenant e ștampilată de DbContext, nu primită aici.</summary>
    public static User Create(string email, string passwordHash, string fullName, UserRole role = UserRole.Member)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);

        return new User
        {
            // Normalizat o singură dată, în domeniu: altfel indexul unic (TenantId, Email)
            // ar lăsa să treacă "George@acme.ro" și "george@acme.ro" ca două conturi.
            Email = email.Trim().ToLowerInvariant(),
            PasswordHash = passwordHash,
            FullName = fullName.Trim(),
            Role = role,
            IsActive = true
        };
    }

    /// <summary>Înregistrează o autentificare reușită.</summary>
    public void RecordSuccessfulLogin()
    {
        LastLoginAtUtc = DateTime.UtcNow;
        MarkAsUpdated();
    }

    /// <summary>Înlocuiește hash-ul parolei.</summary>
    public void ChangePassword(string newPasswordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newPasswordHash);
        PasswordHash = newPasswordHash;
        MarkAsUpdated();
    }

    /// <summary>Schimbă rolul contului.</summary>
    /// <exception cref="InvalidOperationException">Dacă se încearcă acordarea rolului de GlobalAdmin.</exception>
    public void ChangeRole(UserRole role)
    {
        // Fără această verificare, un TenantAdmin cu drept de administrare a userilor
        // și-ar putea acorda GlobalAdmin și ar ajunge la datele tuturor celorlalți clienți.
        if (role == UserRole.GlobalAdmin)
        {
            throw new InvalidOperationException(
                "Rolul de GlobalAdmin nu poate fi acordat prin API.");
        }

        Role = role;
        MarkAsUpdated();
    }

    /// <summary>Dezactivează contul, păstrând istoricul acțiunilor lui.</summary>
    public void Deactivate()
    {
        IsActive = false;
        MarkAsUpdated();
    }

    /// <summary>Reactivează un cont dezactivat.</summary>
    public void Activate()
    {
        IsActive = true;
        MarkAsUpdated();
    }
}
