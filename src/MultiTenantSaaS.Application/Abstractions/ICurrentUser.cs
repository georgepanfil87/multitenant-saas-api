using MultiTenantSaaS.Domain.Enums;

namespace MultiTenantSaaS.Application.Abstractions;

/// <summary>
/// The authenticated user of the current request. Separate from <see cref="ITenantContext"/>:
/// a tenant exists for anonymous requests too, a user does not.
/// </summary>
public interface ICurrentUser
{
    Guid? UserId { get; }

    string? Email { get; }

    UserRole? Role { get; }

    bool IsAuthenticated { get; }
}
