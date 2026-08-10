namespace MultiTenantSaaS.Application.Common;

/// <summary>Numele scurte ale claim-urilor emise de noi.</summary>
/// <remarks>
/// Folosim nume scurte („role", nu URI-ul lung din <c>ClaimTypes</c>) și dezactivăm maparea
/// automată a Microsoft la validare. Rezultatul: ce scriem în token e exact ce citim din el,
/// iar token-ul rămâne mic.
/// </remarks>
public static class ClaimNames
{
    public const string Role = "role";
}
