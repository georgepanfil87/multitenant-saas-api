using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using MultiTenantSaaS.Infrastructure.MultiTenancy;

namespace MultiTenantSaaS.Infrastructure.Persistence;

/// <summary>
/// Permite lui <c>dotnet ef</c> să construiască contextul fără să pornească aplicația.
/// </summary>
/// <remarks>
/// Fără această fabrică, generarea migrărilor ar necesita ca API-ul să pornească complet,
/// inclusiv autentificarea și middleware-ul de tenant - lucruri irelevante pentru o migrare.
/// </remarks>
public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    private const string DefaultDesignTimeConnection =
        "Host=localhost;Port=5432;Database=multitenant_saas;Username=postgres;Password=postgres";

    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? DefaultDesignTimeConnection;

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new ApplicationDbContext(options, new NullTenantContext());
    }
}
