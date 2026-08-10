using MultiTenantSaaS.Domain.Enums;

namespace MultiTenantSaaS.Application.Abstractions;

/// <summary>Utilizatorul autentificat al cererii curente.</summary>
/// <remarks>
/// Separată de <see cref="ITenantContext"/>: tenantul e stabilit și pentru cereri anonime
/// (login), pe când utilizatorul există doar după autentificare.
/// </remarks>
public interface ICurrentUser
{
    Guid? UserId { get; }
    string? Email { get; }
    UserRole? Role { get; }
    bool IsAuthenticated { get; }
}
