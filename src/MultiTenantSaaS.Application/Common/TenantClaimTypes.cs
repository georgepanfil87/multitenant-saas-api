namespace MultiTenantSaaS.Application.Common;

/// <summary>Numele claim-urilor custom din JWT. Emise la Pasul 5, citite la Pasul 4.</summary>
public static class TenantClaimTypes
{
    /// <summary>ID-ul tenantului căruia îi aparține utilizatorul autentificat.</summary>
    public const string TenantId = "tenant_id";

    /// <summary>Slug-ul tenantului, pentru logare și mesaje de eroare.</summary>
    public const string TenantSlug = "tenant_slug";
}
