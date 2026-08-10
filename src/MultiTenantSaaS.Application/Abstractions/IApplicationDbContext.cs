using Microsoft.EntityFrameworkCore;
using MultiTenantSaaS.Domain.Entities;

namespace MultiTenantSaaS.Application.Abstractions;

/// <summary>
/// Accesul la date văzut din stratul Application. Expune doar seturile și salvarea,
/// nu și configurarea sau migrările.
/// </summary>
/// <remarks>
/// Application referă abstracțiile EF Core (<c>DbSet</c>), dar nu și furnizorul concret:
/// nicio referință la Npgsql sau la <c>ApplicationDbContext</c>. Domain rămâne cu zero pachete.
/// Query filter-ul pe tenant se aplică oricum, pentru că trăiește în model, nu în interfață.
/// </remarks>
public interface IApplicationDbContext
{
    DbSet<Tenant> Tenants { get; }
    DbSet<Role> Roles { get; }
    DbSet<User> Users { get; }
    DbSet<Project> Projects { get; }
    DbSet<Ticket> Tickets { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
