using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MultiTenantSaaS.Application.Abstractions;
using MultiTenantSaaS.Infrastructure.Identity;
using MultiTenantSaaS.Infrastructure.MultiTenancy;
using MultiTenantSaaS.Infrastructure.Persistence;

namespace MultiTenantSaaS.Infrastructure;

/// <summary>Single entry point for registering infrastructure services.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "The 'DefaultConnection' connection string is missing from configuration.");

        // Scoped: one tenant per request. A singleton would mix tenants across concurrent
        // requests; transient would give each dependency a different tenant within one request.
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddMemoryCache();
        services.AddScoped<ITenantStore, CachedTenantStore>();

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<ITransactionManager, EfTransactionManager>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();

        return services;
    }
}
