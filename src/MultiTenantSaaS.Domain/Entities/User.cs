using MultiTenantSaaS.Domain.Common;
using MultiTenantSaaS.Domain.Enums;

namespace MultiTenantSaaS.Domain.Entities;

/// <summary>
/// A user account belonging to exactly one organization. Email uniqueness is
/// (TenantId, Email), not global: the same person can hold accounts at several clients.
/// </summary>
public sealed class User : BaseEntity, ITenantEntity
{
    private User()
    {
        Email = string.Empty;
        PasswordHash = string.Empty;
        FullName = string.Empty;
    }

    public Guid TenantId { get; private set; }

    public string Email { get; private set; }

    /// <summary>Password hash. The plaintext password never reaches this entity.</summary>
    public string PasswordHash { get; private set; }

    public string FullName { get; private set; }

    /// <summary>Access level. Doubles as the foreign key to the Roles table.</summary>
    public UserRole Role { get; private set; } = UserRole.Member;

    public bool IsActive { get; private set; } = true;

    public DateTime? LastLoginAtUtc { get; private set; }

    public Tenant? Tenant { get; private set; }

    /// <summary>Creates an account. Tenant ownership is stamped on save, not passed in here.</summary>
    public static User Create(string email, string passwordHash, string fullName, UserRole role = UserRole.Member)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);

        return new User
        {
            // Normalized once, here: otherwise the (TenantId, Email) unique index would let
            // "George@acme.ro" and "george@acme.ro" through as two separate accounts.
            Email = email.Trim().ToLowerInvariant(),
            PasswordHash = passwordHash,
            FullName = fullName.Trim(),
            Role = role,
            IsActive = true
        };
    }

    public void RecordSuccessfulLogin()
    {
        LastLoginAtUtc = DateTime.UtcNow;
        MarkAsUpdated();
    }

    public void ChangePassword(string newPasswordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newPasswordHash);
        PasswordHash = newPasswordHash;
        MarkAsUpdated();
    }

    /// <exception cref="InvalidOperationException">Granting GlobalAdmin is never allowed.</exception>
    public void ChangeRole(UserRole role)
    {
        // Without this, a TenantAdmin who can manage users could grant themselves GlobalAdmin
        // and reach every other client's data.
        if (role == UserRole.GlobalAdmin)
        {
            throw new InvalidOperationException(
                "The GlobalAdmin role cannot be granted through the API.");
        }

        Role = role;
        MarkAsUpdated();
    }

    public void Deactivate()
    {
        IsActive = false;
        MarkAsUpdated();
    }

    public void Activate()
    {
        IsActive = true;
        MarkAsUpdated();
    }
}
