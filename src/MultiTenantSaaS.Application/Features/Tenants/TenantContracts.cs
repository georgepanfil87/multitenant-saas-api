using System.ComponentModel.DataAnnotations;
using MultiTenantSaaS.Application.Features.Authentication;
using MultiTenantSaaS.Domain.Enums;

namespace MultiTenantSaaS.Application.Features.Tenants;

/// <summary>Cerere de înregistrare a unei organizații noi, împreună cu primul administrator.</summary>
public sealed record RegisterTenantRequest
{
    /// <summary>Numele afișat al organizației. Ex: „Acme Corporation".</summary>
    [Required]
    [MaxLength(200)]
    public string OrganizationName { get; init; } = string.Empty;

    /// <summary>
    /// Identificator URL-safe, unic pe platformă. Devine valoarea headerului <c>X-Tenant</c>.
    /// </summary>
    [Required]
    [MinLength(3)]
    [MaxLength(63)]
    [RegularExpression("^[a-z0-9]([a-z0-9-]{1,61}[a-z0-9])$",
        ErrorMessage = "Slug-ul acceptă doar litere mici, cifre și cratime, între 3 și 63 de caractere.")]
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

/// <summary>Rezumatul unei organizații.</summary>
public sealed record TenantSummary(
    Guid Id,
    string Slug,
    string Name,
    SubscriptionPlan Plan,
    bool IsActive,
    DateTime CreatedAtUtc);

/// <summary>
/// Rezultatul onboarding-ului. Include un token valid, ca aplicația client să continue
/// direct, fără un login separat imediat după înregistrare.
/// </summary>
public sealed record TenantRegistrationResponse(
    TenantSummary Tenant,
    UserResponse Admin,
    string AccessToken,
    string TokenType,
    DateTime ExpiresAtUtc,
    SeededData Seeded);

/// <summary>Ce s-a creat automat în organizația nouă, ca să nu fie goală la prima deschidere.</summary>
public sealed record SeededData(Guid ProjectId, string ProjectCode, Guid WelcomeTicketId);
