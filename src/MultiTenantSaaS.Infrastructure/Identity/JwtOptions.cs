using System.ComponentModel.DataAnnotations;

namespace MultiTenantSaaS.Infrastructure.Identity;

/// <summary>Configurarea emiterii și validării JWT, din secțiunea <c>Jwt</c>.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>Lungimea minimă a cheii pentru HMAC-SHA256, în caractere.</summary>
    public const int MinimumKeyLength = 32;

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// Cheia simetrică de semnare. Nu are valoare implicită, intenționat: o cheie „de
    /// dezvoltare" strecurată în producție e o vulnerabilitate tăcută, iar o aplicație
    /// care refuză să pornească fără cheie e un incident de cinci minute la deploy.
    /// </summary>
    [Required]
    [MinLength(MinimumKeyLength)]
    public string SigningKey { get; set; } = string.Empty;

    [Range(1, 1440)]
    public int AccessTokenMinutes { get; set; } = 60;
}
