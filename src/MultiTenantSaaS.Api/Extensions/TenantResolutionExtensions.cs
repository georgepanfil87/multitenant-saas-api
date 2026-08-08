using MultiTenantSaaS.Api.Middleware;
using MultiTenantSaaS.Api.MultiTenancy;

namespace MultiTenantSaaS.Api.Extensions;

public static class TenantResolutionExtensions
{
    public static IServiceCollection AddTenantResolution(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<TenantResolutionOptions>(
            configuration.GetSection(TenantResolutionOptions.SectionName));

        // Ordinea înregistrării contează: middleware-ul consumă IEnumerable<ITenantResolutionStrategy>
        // și ia prima strategie non-token care întoarce ceva.
        services.AddSingleton<ITenantResolutionStrategy, ClaimTenantResolutionStrategy>();
        services.AddSingleton<ITenantResolutionStrategy, HeaderTenantResolutionStrategy>();
        services.AddSingleton<ITenantResolutionStrategy, SubdomainTenantResolutionStrategy>();

        return services;
    }

    /// <summary>
    /// Trebuie apelat <b>după</b> <c>UseAuthentication()</c>, altfel claim-urile nu sunt încă
    /// populate și tenantul ar fi luat din header chiar și pentru cereri autentificate.
    /// </summary>
    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder app) =>
        app.UseMiddleware<TenantResolutionMiddleware>();
}
