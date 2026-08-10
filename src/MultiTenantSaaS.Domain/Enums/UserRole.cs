namespace MultiTenantSaaS.Domain.Enums;

/// <summary>Access levels available across the platform.</summary>
public enum UserRole
{
    // Values are explicit because they are persisted as Users.RoleId.
    // Never reorder or reuse a value: it would silently change existing users' roles.

    /// <summary>Platform administrator. The only role that can cross tenant boundaries.</summary>
    GlobalAdmin = 1,

    /// <summary>Administrator of a client organization, scoped to that organization.</summary>
    TenantAdmin = 2,

    /// <summary>Regular member of a client organization.</summary>
    Member = 3
}
