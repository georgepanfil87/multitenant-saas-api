using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MultiTenantSaaS.Application.Abstractions;
using MultiTenantSaaS.Infrastructure.MultiTenancy;
using MultiTenantSaaS.Infrastructure.Persistence;

namespace MultiTenantSaaS.Infrastructure;

/// <summary>Punctul unic prin care API-ul înregistrează serviciile de infrastructură.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string-ul 'DefaultConnection' lipsește din configurație.");

        // Scoped: un tenant per request. Singleton ar amesteca tenanții între cereri
        // concurente, iar Transient ar da fiecărei dependințe alt tenant în același request.
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddMemoryCache();
        services.AddScoped<ITenantStore, CachedTenantStore>();

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));

        return services;
    }
}
