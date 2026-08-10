using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MultiTenantSaaS.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace MultiTenantSaaS.IntegrationTests;

/// <summary>
/// Boots the full API against a real PostgreSQL running in a throwaway container. Unit tests
/// use the in-memory provider, which is fast but has no SQL, foreign keys or transactions.
/// These tests also go through the complete HTTP pipeline, covering model binding,
/// authentication and the tenant middleware.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("multitenant_saas_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        // Migrate the fresh database: this also proves the migrations themselves run, not just
        // that the EF model is self-consistent.
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment("Testing");

        // UseSetting, not ConfigureAppConfiguration: with minimal hosting, Program.cs reads
        // configuration during its own execution, before ConfigureAppConfiguration delegates run.
        // UseSetting writes straight into host configuration, available from the start.
        builder.UseSetting("ConnectionStrings:DefaultConnection", _postgres.GetConnectionString());
        builder.UseSetting("Jwt:Issuer", "MultiTenantSaaS.Tests");
        builder.UseSetting("Jwt:Audience", "MultiTenantSaaS.Tests.Clients");
        builder.UseSetting("Jwt:SigningKey", "cheie-de-test-suficient-de-lunga-pentru-hmac-sha256");
        builder.UseSetting("Jwt:AccessTokenMinutes", "60");

        // Deliberately high quotas: these tests verify isolation, not throttling. Rate limiting
        // has its own tests; here it would only make results depend on test ordering.
        builder.UseSetting("RateLimiting:FreePerMinute", "100000");
        builder.UseSetting("RateLimiting:AnonymousPerMinute", "100000");
        builder.UseSetting("RateLimiting:RegistrationsPerHour", "1000");
    }
}
