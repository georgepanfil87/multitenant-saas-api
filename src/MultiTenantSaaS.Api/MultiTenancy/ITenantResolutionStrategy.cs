namespace MultiTenantSaaS.Api.MultiTenancy;

/// <summary>De unde provine identificatorul de tenant. Determină cât de mult îl credem.</summary>
public enum TenantIdentifierSource
{
    /// <summary>Din claim-ul unui JWT semnat de noi. Singura sursă care nu poate fi falsificată.</summary>
    Token = 1,

    /// <summary>Dintr-un header HTTP. Controlat integral de client.</summary>
    Header = 2,

    /// <summary>Din subdomeniul cererii. Controlat de client, dar constrâns de DNS.</summary>
    Subdomain = 3
}

/// <summary>Extrage identificatorul de tenant dintr-un request HTTP.</summary>
public interface ITenantResolutionStrategy
{
    /// <summary>Sursa acoperită de această strategie.</summary>
    TenantIdentifierSource Source { get; }

    /// <summary>Identificatorul găsit, sau <c>null</c> dacă strategia nu se aplică cererii.</summary>
    string? TryResolve(HttpContext context);
}
