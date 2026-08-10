using System.ComponentModel.DataAnnotations;
using MultiTenantSaaS.Domain.Enums;

namespace MultiTenantSaaS.Application.Features.Authentication;

/// <summary>Cerere de autentificare. Organizația vine din contextul cererii, nu din corp.</summary>
/// <remarks>
/// Deliberat nu există un câmp <c>tenant</c> aici. Tenantul se stabilește în middleware
/// (header sau subdomeniu), într-un singur loc pentru toată aplicația. Dacă l-am accepta și
/// din corpul cererii, am avea două surse de adevăr care se pot contrazice.
/// </remarks>
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

/// <summary>Tokenul emis și datele utilizatorului autentificat.</summary>
public sealed record AuthResponse(
    string AccessToken,
    string TokenType,
    DateTime ExpiresAtUtc,
    UserResponse User);

/// <summary>Proiecție publică a unui utilizator. Nu conține niciodată hash-ul parolei.</summary>
public sealed record UserResponse(
    Guid Id,
    string Email,
    string FullName,
    UserRole Role,
    bool IsActive,
    DateTime? LastLoginAtUtc);

/// <summary>Cerere de creare a unui utilizator în organizația curentă.</summary>
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

    /// <summary>Rolul acordat. <see cref="UserRole.GlobalAdmin"/> este respins.</summary>
    public UserRole Role { get; init; } = UserRole.Member;
}
