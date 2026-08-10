using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using MultiTenantSaaS.Application.Abstractions;
using MultiTenantSaaS.Application.Common;
using MultiTenantSaaS.Domain.Enums;

namespace MultiTenantSaaS.Api.Identity;

/// <summary>Citește utilizatorul autentificat din claim-urile cererii curente.</summary>
public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid? UserId =>
        Guid.TryParse(Principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id) ? id : null;

    public string? Email => Principal?.FindFirst(JwtRegisteredClaimNames.Email)?.Value;

    public UserRole? Role =>
        Enum.TryParse<UserRole>(Principal?.FindFirst(ClaimNames.Role)?.Value, out var role) ? role : null;
}
