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

        // Registration order matters: the middleware takes the first non-token strategy that
        // returns a value.
        services.AddSingleton<ITenantResolutionStrategy, ClaimTenantResolutionStrategy>();
        services.AddSingleton<ITenantResolutionStrategy, HeaderTenantResolutionStrategy>();
        services.AddSingleton<ITenantResolutionStrategy, SubdomainTenantResolutionStrategy>();

        return services;
    }

    /// <summary>
    /// Must be called after UseAuthentication(), otherwise claims are not populated yet and the
    /// tenant would come from the header even for authenticated requests.
    /// </summary>
    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder app) =>
        app.UseMiddleware<TenantResolutionMiddleware>();
}
