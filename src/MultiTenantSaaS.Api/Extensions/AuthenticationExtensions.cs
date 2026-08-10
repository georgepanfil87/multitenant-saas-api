using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using MultiTenantSaaS.Api.Identity;
using MultiTenantSaaS.Application.Abstractions;
using MultiTenantSaaS.Application.Common;
using MultiTenantSaaS.Domain.Enums;
using MultiTenantSaaS.Infrastructure.Identity;

namespace MultiTenantSaaS.Api.Extensions;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            // Validated at startup, not on first login: a missing key stops the deploy instead
            // of producing 500s when the first user arrives.
            .ValidateOnStart();

        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("The 'Jwt' configuration section is missing.");

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // No inbound claim mapping: "role" stays "role" instead of becoming a long
                // schema.xmlsoap.org URI. What we put in the token is what we read back.
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ValidateLifetime = true,

                    // .NET accepts expired tokens for another 5 minutes by default. Reduced to
                    // 30 seconds: the tolerance exists for clock drift, not to extend a token's life.
                    ClockSkew = TimeSpan.FromSeconds(30),

                    NameClaimType = JwtRegisteredClaimNames.Sub,
                    RoleClaimType = ClaimNames.Role
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(AuthorizationPolicies.GlobalAdmin, policy =>
                policy.RequireRole(nameof(UserRole.GlobalAdmin)))

            // Policies are cumulative: a platform admin can do everything a tenant admin can.
            // Listed explicitly rather than derived from an implicit hierarchy, so a security
            // audit reads the list instead of inferring the rule.
            .AddPolicy(AuthorizationPolicies.TenantAdmin, policy =>
                policy.RequireRole(nameof(UserRole.GlobalAdmin), nameof(UserRole.TenantAdmin)))

            .AddPolicy(AuthorizationPolicies.Member, policy =>
                policy.RequireRole(
                    nameof(UserRole.GlobalAdmin),
                    nameof(UserRole.TenantAdmin),
                    nameof(UserRole.Member)));

        return services;
    }
}
