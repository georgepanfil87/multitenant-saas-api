using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using MultiTenantSaaS.Infrastructure.MultiTenancy;

namespace MultiTenantSaaS.Infrastructure.Persistence;

/// <summary>
/// Lets `dotnet ef` build the context without starting the application. Without it, generating
/// a migration would require the full API to boot, authentication and tenant middleware included.
/// </summary>
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
