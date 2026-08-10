namespace MultiTenantSaaS.Application.Common;

/// <summary>Authorization policy names, as constants so a typo fails the build.</summary>
public static class AuthorizationPolicies
{
    /// <summary>Platform administrators only.</summary>
    public const string GlobalAdmin = "GlobalAdmin";

    /// <summary>Organization administrators, plus platform administrators.</summary>
    public const string TenantAdmin = "TenantAdmin";

    /// <summary>Any authenticated user with a valid role.</summary>
    public const string Member = "Member";
}
