using System.ComponentModel.DataAnnotations;
using MultiTenantSaaS.Domain.Enums;

namespace MultiTenantSaaS.Application.Features.Authentication;

/// <summary>
/// Login request. The organization comes from the request context (X-Tenant header or
/// subdomain), never from the body, so there is a single source of truth.
/// </summary>
public sealed record LoginRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(320)]
    public string Email { get; init; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string Password { get; init; } = string.Empty;
}

/// <summary>Issued token plus the authenticated user.</summary>
public sealed record AuthResponse(
    string AccessToken,
    string TokenType,
    DateTime ExpiresAtUtc,
    UserResponse User);

/// <summary>Public view of a user. Never contains the password hash.</summary>
public sealed record UserResponse(
    Guid Id,
    string Email,
    string FullName,
    UserRole Role,
    bool IsActive,
    DateTime? LastLoginAtUtc);

/// <summary>Creates a user inside the caller's organization.</summary>
public sealed record CreateUserRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(320)]
    public string Email { get; init; } = string.Empty;

    [Required]
    [MinLength(8)]
    [MaxLength(128)]
    public string Password { get; init; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string FullName { get; init; } = string.Empty;

    /// <summary>Role to grant. GlobalAdmin is rejected.</summary>
    public UserRole Role { get; init; } = UserRole.Member;
}
