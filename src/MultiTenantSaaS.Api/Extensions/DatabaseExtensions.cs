using Microsoft.EntityFrameworkCore;
using MultiTenantSaaS.Application.Features.Tenants;
using MultiTenantSaaS.Infrastructure.Persistence;

namespace MultiTenantSaaS.Api.Extensions;

public static class DatabaseExtensions
{
    /// <summary>
    /// Applies pending migrations at startup when Database:AutoMigrate is enabled. Convenient for
    /// `docker compose up`, but with several replicas they all migrate at once on deploy: EF Core
    /// takes a migration lock so the schema stays intact, yet startup serializes. Hence a switch
    /// rather than a default: in production, migrate as a separate deploy step.
    /// </summary>
    public static async Task MigrateDatabaseAsync(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        if (!app.Configuration.GetValue("Database:AutoMigrate", defaultValue: false))
        {
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await db.Database.MigrateAsync();
    }

    /// <summary>
    /// Seeds demo organizations when Database:SeedDemoData is enabled. Kept separate from
    /// migration because it answers a different question: migrating a schema is safe anywhere,
    /// writing test data is not.
    /// </summary>
    public static async Task SeedDemoDataAsync(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        if (!app.Configuration.GetValue("Database:SeedDemoData", defaultValue: false))
        {
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IDemoDataSeeder>()
            .SeedAsync();
    }
}
