using Microsoft.AspNetCore.HttpOverrides;
using MultiTenantSaaS.Api.Extensions;
using MultiTenantSaaS.Api.Middleware;
using MultiTenantSaaS.Application;
using MultiTenantSaaS.Application.Abstractions;
using MultiTenantSaaS.Application.Common;
using MultiTenantSaaS.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();
builder.Services.AddTenantResolution(builder.Configuration);
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddTenantRateLimiting(builder.Configuration);

builder.Services.AddExceptionHandler<AppExceptionHandler>();
builder.Services.AddProblemDetails();

// Behind a reverse proxy, RemoteIpAddress would otherwise be the proxy's address, putting
// every anonymous client into the same rate-limiting partition.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddControllers();
builder.Services.AddSwaggerDocumentation();

var app = builder.Build();

await app.MigrateDatabaseAsync();
await app.SeedDemoDataAsync();

app.UseForwardedHeaders();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment() || app.Configuration.GetValue("Swagger:Enabled", false))
{
    app.UseSwaggerDocumentation();
}

// HTTPS redirection is off by default: in a container TLS terminates at the proxy and a
// redirect here would loop. Enable it where the app serves HTTPS directly.
if (app.Configuration.GetValue("EnableHttpsRedirection", false))
{
    app.UseHttpsRedirection();
}

// This order is part of the security model: UseAuthentication populates the claims, then
// UseTenantResolution reads them and sets the tenant, then UseAuthorization decides access,
// so endpoints already run inside a tenant context.
app.UseAuthentication();
app.UseTenantResolution();

// After tenant resolution: the limiter needs the organization's plan to pick a quota.
app.UseRateLimiter();

app.UseAuthorization();

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "MultiTenantSaaS.Api" }))
   .WithName("HealthCheck")
   .WithTags("System");

app.MapGet("/api/tenant/current", (ITenantContext tenantContext) => Results.Ok(new
    {
        tenantId = tenantContext.TenantId,
        slug = tenantContext.TenantSlug
    }))
   .RequireAuthorization(AuthorizationPolicies.Member)
   .WithName("CurrentTenant")
   .WithTags("System");

await app.RunAsync();

/// <summary>Exposed for integration tests (WebApplicationFactory).</summary>
public partial class Program;
