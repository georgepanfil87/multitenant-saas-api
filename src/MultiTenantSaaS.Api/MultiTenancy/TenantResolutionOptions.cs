namespace MultiTenantSaaS.Api.MultiTenancy;

/// <summary>Configurarea rezoluției de tenant, din secțiunea <c>MultiTenancy</c>.</summary>
public sealed class TenantResolutionOptions
{
    public const string SectionName = "MultiTenancy";

    /// <summary>Headerul din care se citește slug-ul tenantului.</summary>
    public string HeaderName { get; set; } = "X-Tenant";

    /// <summary>Dacă strategia bazată pe header este activă.</summary>
    public bool EnableHeaderStrategy { get; set; } = true;

    /// <summary>Dacă strategia bazată pe subdomeniu este activă.</summary>
    public bool EnableSubdomainStrategy { get; set; }

    /// <summary>
    /// Domeniul de bază față de care se extrage subdomeniul. Ex: <c>api.exemplu.ro</c>,
    /// pentru care <c>acme.api.exemplu.ro</c> dă slug-ul <c>acme</c>.
    /// </summary>
    public string? BaseDomain { get; set; }

    /// <summary>Căi care nu au nevoie de tenant și nu trebuie să eșueze din cauza lui.</summary>
    public string[] SkipPaths { get; set; } = ["/health", "/swagger"];
}
