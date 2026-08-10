namespace MultiTenantSaaS.Api.MultiTenancy;

/// <summary>Where a tenant identifier came from. Determines how much it is trusted.</summary>
public enum TenantIdentifierSource
{
    /// <summary>A claim in a JWT we signed. The only source that cannot be forged.</summary>
    Token = 1,

    /// <summary>An HTTP header. Fully controlled by the client.</summary>
    Header = 2,

    /// <summary>The request subdomain. Client-controlled, but constrained by DNS.</summary>
    Subdomain = 3
}

/// <summary>Extracts a tenant identifier from an HTTP request.</summary>
public interface ITenantResolutionStrategy
{
    /// <summary>The source this strategy covers.</summary>
    TenantIdentifierSource Source { get; }

    /// <summary>The identifier found, or null if the strategy does not apply.</summary>
    string? TryResolve(HttpContext context);
}
