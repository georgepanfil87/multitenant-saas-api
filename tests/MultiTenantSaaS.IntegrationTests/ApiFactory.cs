using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MultiTenantSaaS.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace MultiTenantSaaS.IntegrationTests;

/// <summary>
/// Pornește API-ul complet peste un PostgreSQL real, rulat într-un container efemer.
/// </summary>
/// <remarks>
/// <para>
/// Testele unitare folosesc provider-ul in-memory, care e rapid dar minte: nu are SQL,
/// nu are chei străine, nu are tranzacții. Aici rulăm exact motorul din producție, deci
/// prindem clasa de probleme care apar doar la traducerea în SQL - proiecții netraductibile,
/// constrângeri compuse, comportamentul <c>timestamptz</c>.
/// </para>
/// <para>
/// Aceste teste trec și prin pipeline-ul HTTP complet, deci acoperă și model binding-ul,
/// autentificarea și middleware-ul de tenant - lucruri invizibile pentru un test unitar.
/// </para>
/// </remarks>
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

        // Aplicăm migrările pe baza de date proaspătă: testăm și că migrările chiar rulează,
        // nu doar că modelul EF e coerent.
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

        // UseSetting, nu ConfigureAppConfiguration: la minimal hosting, Program.cs citește
        // configurația în timpul propriei execuții, adică ÎNAINTE ca delegatele de
        // ConfigureAppConfiguration să apuce să ruleze. UseSetting scrie direct în
        // configurația gazdei, disponibilă de la bun început.
        builder.UseSetting("ConnectionStrings:DefaultConnection", _postgres.GetConnectionString());
        builder.UseSetting("Jwt:Issuer", "MultiTenantSaaS.Tests");
        builder.UseSetting("Jwt:Audience", "MultiTenantSaaS.Tests.Clients");
        builder.UseSetting("Jwt:SigningKey", "cheie-de-test-suficient-de-lunga-pentru-hmac-sha256");
        builder.UseSetting("Jwt:AccessTokenMinutes", "60");

        // Cote mari intenționat: aceste teste verifică izolarea, nu limitarea. Rate
        // limiting-ul are teste proprii pe rezolvarea cotei; aici ar produce doar teste
        // instabile, dependente de câte cereri a făcut testul anterior.
        builder.UseSetting("RateLimiting:FreePerMinute", "100000");
        builder.UseSetting("RateLimiting:AnonymousPerMinute", "100000");
        builder.UseSetting("RateLimiting:RegistrationsPerHour", "1000");
    }
}
