using Microsoft.Extensions.DependencyInjection;
using MultiTenantSaaS.Application.Features.Authentication;
using MultiTenantSaaS.Application.Features.Projects;
using MultiTenantSaaS.Application.Features.Tenants;
using MultiTenantSaaS.Application.Features.Tickets;

namespace MultiTenantSaaS.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ITenantOnboardingService, TenantOnboardingService>();
        services.AddScoped<IDemoDataSeeder, DemoDataSeeder>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<ITicketService, TicketService>();
        return services;
    }
}
