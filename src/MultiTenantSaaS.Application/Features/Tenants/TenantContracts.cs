using System.ComponentModel.DataAnnotations;
using MultiTenantSaaS.Application.Features.Authentication;
using MultiTenantSaaS.Domain.Enums;

namespace MultiTenantSaaS.Application.Features.Tenants;

/// <summary>Registers a new organization together with its first administrator.</summary>
public sealed record RegisterTenantRequest
{
    [Required]
    [MaxLength(200)]
    public string OrganizationName { get; init; } = string.Empty;

    /// <summary>URL-safe identifier, unique platform-wide. Becomes the X-Tenant header value.</summary>
    [Required]
    [MinLength(3)]
    [MaxLength(63)]
    [RegularExpression("^[a-z0-9]([a-z0-9-]{1,61}[a-z0-9])$",
        ErrorMessage = "The slug accepts only lowercase letters, digits and hyphens, 3 to 63 characters.")]
    public string Slug { get; init; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(320)]
    public string AdminEmail { get; init; } = string.Empty;

    [Required]
    [MinLength(8)]
    [MaxLength(128)]
    public string AdminPassword { get; init; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string AdminFullName { get; init; } = string.Empty;
}

public sealed record TenantSummary(
    Guid Id,
    string Slug,
    string Name,
    SubscriptionPlan Plan,
    bool IsActive,
    DateTime CreatedAtUtc);

/// <summary>
/// Onboarding result. Includes a valid token so the client can continue without a separate
/// login round-trip.
/// </summary>
public sealed record TenantRegistrationResponse(
    TenantSummary Tenant,
    UserResponse Admin,
    string AccessToken,
    string TokenType,
    DateTime ExpiresAtUtc,
    SeededData Seeded);

/// <summary>What was created automatically, so the new organization is not empty.</summary>
public sealed record SeededData(Guid ProjectId, string ProjectCode, Guid WelcomeTicketId);
