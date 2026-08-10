using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MultiTenantSaaS.Application.Abstractions;
using MultiTenantSaaS.Application.Common;
using MultiTenantSaaS.Domain.Entities;

namespace MultiTenantSaaS.Infrastructure.Identity;

/// <summary>Issues symmetrically signed JWTs (HMAC-SHA256).</summary>
public sealed class JwtTokenGenerator(IOptions<JwtOptions> options) : IJwtTokenGenerator
{
    public GeneratedToken Generate(User user, string tenantSlug)
    {
        ArgumentNullException.ThrowIfNull(user);

        var settings = options.Value;
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(settings.AccessTokenMinutes);

        // tenant_id in the token is what makes isolation unforgeable: the resolution middleware
        // prefers it over any header, and the client cannot change it without breaking the signature.
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Name, user.FullName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(TenantClaimTypes.TenantId, user.TenantId.ToString()),
            new(TenantClaimTypes.TenantSlug, tenantSlug),
            new(ClaimNames.Role, user.Role.ToString())
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: settings.Issuer,
            audience: settings.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        return new GeneratedToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc);
    }
}
