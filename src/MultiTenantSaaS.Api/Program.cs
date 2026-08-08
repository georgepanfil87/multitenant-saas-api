using MultiTenantSaaS.Api.Extensions;
using MultiTenantSaaS.Application.Abstractions;
using MultiTenantSaaS.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddTenantResolution(builder.Configuration);

// Fără schemă configurată deocamdată; UseAuthentication ar arunca la pornire fără
// această înregistrare. JWT Bearer se adaugă la Pasul 5.
builder.Services.AddAuthentication();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Ordinea de aici este parte din modelul de securitate:
// UseAuthentication (Pas 5) populează claim-urile -> UseTenantResolution le poate citi
// și stabilește tenantul -> UseAuthorization decide accesul -> endpoint-urile lucrează
// deja într-un context de tenant.
app.UseAuthentication();
app.UseTenantResolution();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "MultiTenantSaaS.Api" }))
   .WithName("HealthCheck")
   .WithTags("System");

// Endpoint de diagnostic: arată ce tenant a rezolvat middleware-ul pentru cererea curentă.
// Se restrânge la GlobalAdmin la Pasul 5, când există autorizare.
app.MapGet("/api/tenant/current", (ITenantContext tenantContext) => tenantContext.IsResolved
        ? Results.Ok(new { tenantId = tenantContext.TenantId, slug = tenantContext.TenantSlug })
        : Results.Ok(new { tenantId = (Guid?)null, slug = (string?)null }))
   .WithName("CurrentTenant")
   .WithTags("System");

app.Run();

/// <summary>Expus pentru testele de integrare (WebApplicationFactory).</summary>
public partial class Program;
