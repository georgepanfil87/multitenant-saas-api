namespace MultiTenantSaaS.Application.Common;

/// <summary>
/// Numele policy-urilor de autorizare. Constante, ca o greșeală de scriere să fie eroare
/// de compilare, nu un endpoint care rămâne accidental deschis.
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>Doar administratori de platformă.</summary>
    public const string GlobalAdmin = "GlobalAdmin";

    /// <summary>Administratori de organizație și, implicit, administratorii de platformă.</summary>
    public const string TenantAdmin = "TenantAdmin";

    /// <summary>Orice utilizator autentificat, cu un rol valid.</summary>
    public const string Member = "Member";
}
