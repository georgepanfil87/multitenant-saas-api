using System.ComponentModel.DataAnnotations;

namespace MultiTenantSaaS.Infrastructure.Identity;

/// <summary>JWT issuing and validation settings, bound from the "Jwt" configuration section.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>Minimum key length for HMAC-SHA256, in characters.</summary>
    public const int MinimumKeyLength = 32;

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// Symmetric signing key. Deliberately without a default: a development key leaking into
    /// production is a silent vulnerability, while a missing key is a five-minute deploy issue.
    /// </summary>
    [Required]
    [MinLength(MinimumKeyLength)]
    public string SigningKey { get; set; } = string.Empty;

    [Range(1, 1440)]
    public int AccessTokenMinutes { get; set; } = 60;
}
