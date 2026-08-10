using Microsoft.EntityFrameworkCore;
using MultiTenantSaaS.Domain.Entities;

namespace MultiTenantSaaS.Application.Abstractions;

/// <summary>
/// Data access as seen from the Application layer. Depends on EF Core abstractions only:
/// no Npgsql, no concrete DbContext. Tenant query filters still apply, they live in the model.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<Tenant> Tenants { get; }
    DbSet<Role> Roles { get; }
    DbSet<User> Users { get; }
    DbSet<Project> Projects { get; }
    DbSet<Ticket> Tickets { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
