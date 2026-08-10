namespace MultiTenantSaaS.Application.Common;

/// <summary>
/// Short claim names. Microsoft's inbound claim mapping is disabled, so what we write into
/// the token is exactly what we read back.
/// </summary>
public static class ClaimNames
{
    public const string Role = "role";
}
