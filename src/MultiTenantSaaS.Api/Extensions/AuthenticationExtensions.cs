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
            // Validarea rulează la pornire, nu la primul login: o cheie lipsă oprește
            // deploy-ul, în loc să producă erori 500 abia când intră primul utilizator.
            .ValidateOnStart();

        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("Secțiunea de configurare 'Jwt' lipsește.");

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Fără maparea automată a Microsoft: "role" rămâne "role", nu devine
                // un URI lung de schema.xmlsoap.org. Ce punem în token e ce citim din el.
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

                    // Implicit, .NET acceptă token-uri expirate încă 5 minute. Le reducem
                    // la 30 de secunde: toleranța există pentru derapaj de ceas între servere,
                    // nu ca să prelungească viața unui token revocat.
                    ClockSkew = TimeSpan.FromSeconds(30),

                    NameClaimType = JwtRegisteredClaimNames.Sub,
                    RoleClaimType = ClaimNames.Role
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(AuthorizationPolicies.GlobalAdmin, policy =>
                policy.RequireRole(nameof(UserRole.GlobalAdmin)))

            // Policy-urile sunt cumulative: administratorul de platformă poate face tot ce
            // poate un administrator de organizație. Le enumerăm explicit în loc să ne bazăm
            // pe o ierarhie implicită - la un audit de securitate vrei să citești lista,
            // nu să deduci regula.
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
