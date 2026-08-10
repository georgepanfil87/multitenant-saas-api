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

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Ordinea de aici este parte din modelul de securitate:
// UseAuthentication populează claim-urile -> UseTenantResolution le poate citi și
// stabilește tenantul -> UseAuthorization decide accesul -> endpoint-urile lucrează
// deja într-un context de tenant.
app.UseAuthentication();
app.UseTenantResolution();

// După rezoluția tenantului: limitatorul are nevoie de planul organizației ca să știe
// ce cotă să aplice. Consecința asumată este că o cerere respinsă a costat deja
// validarea tokenului și o căutare de tenant (servită din cache).
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

app.Run();

/// <summary>Expus pentru testele de integrare (WebApplicationFactory).</summary>
public partial class Program;
