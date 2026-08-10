using MultiTenantSaaS.Domain.Entities;

namespace MultiTenantSaaS.Application.Abstractions;

public interface IJwtTokenGenerator
{
    /// <summary>Issues a JWT carrying both the user's identity and their tenant.</summary>
    GeneratedToken Generate(User user, string tenantSlug);
}

public sealed record GeneratedToken(string AccessToken, DateTime ExpiresAtUtc);
