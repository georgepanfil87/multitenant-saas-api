using System.Reflection;
using Microsoft.EntityFrameworkCore;
using MultiTenantSaaS.Application.Abstractions;
using MultiTenantSaaS.Domain.Common;
using MultiTenantSaaS.Domain.Entities;
using MultiTenantSaaS.Infrastructure.Persistence.Converters;

namespace MultiTenantSaaS.Infrastructure.Persistence;

/// <summary>
/// The application's EF Core context. Applies tenant isolation automatically for every
/// <see cref="ITenantEntity"/>: filters on read, stamps TenantId on write.
/// </summary>
public sealed class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options,
    ITenantContext tenantContext) : DbContext(options), IApplicationDbContext
{
    private static readonly MethodInfo ApplyTenantFilterMethod =
        typeof(ApplicationDbContext).GetMethod(
            nameof(ApplyTenantFilter),
            BindingFlags.Instance | BindingFlags.NonPublic)!;

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Ticket> Tickets => Set<Ticket>();

    /// <summary>
    /// Tenant used by the query filter. <see cref="Guid.Empty"/> when none was resolved, which
    /// matches no row: the system fails closed, not open.
    /// </summary>
    public Guid CurrentTenantId => tenantContext.TenantId ?? Guid.Empty;

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // The filter is never written per entity. Everything implementing ITenantEntity is
        // discovered by reflection, so a new entity cannot be left unfiltered by accident.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
            {
                ApplyTenantFilterMethod
                    .MakeGenericMethod(entityType.ClrType)
                    .Invoke(this, [modelBuilder]);
            }
        }

        base.OnModelCreating(modelBuilder);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampTenantAndAudit();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        StampTenantAndAudit();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    // The expression references CurrentTenantId, a context member: EF Core turns it into a SQL
    // parameter re-evaluated per query, not a constant frozen into the cached model.
    private void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantEntity
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
    }

    private void StampTenantAndAudit()
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Property(nameof(BaseEntity.UpdatedAtUtc)).CurrentValue = now;
            }
        }

        foreach (var entry in ChangeTracker.Entries<ITenantEntity>())
        {
            var tenantProperty = entry.Property(nameof(ITenantEntity.TenantId));

            switch (entry.State)
            {
                case EntityState.Added:
                    var currentTenantId = tenantContext.TenantId
                        ?? throw new InvalidOperationException(
                            $"Cannot save {entry.Entity.GetType().Name}: " +
                            "no tenant resolved for the current operation.");

                    var assignedTenantId = (Guid)tenantProperty.CurrentValue!;
                    if (assignedTenantId == Guid.Empty)
                    {
                        tenantProperty.CurrentValue = currentTenantId;
                    }
                    else if (assignedTenantId != currentTenantId)
                    {
                        throw new InvalidOperationException(
                            $"Attempted to write a {entry.Entity.GetType().Name} into tenant " +
                            $"{assignedTenantId}, but the current context is {currentTenantId}.");
                    }

                    break;

                case EntityState.Modified:
                case EntityState.Deleted:
                    // Catches an attempt to move a row between tenants. Rows arrive here already
                    // filtered, so this should be unreachable: if it fires, something deeper is broken.
                    if (!Equals(tenantProperty.OriginalValue, tenantProperty.CurrentValue))
                    {
                        throw new InvalidOperationException(
                            $"TenantId cannot be changed on {entry.Entity.GetType().Name} " +
                            $"({tenantProperty.OriginalValue} -> {tenantProperty.CurrentValue}).");
                    }

                    break;
            }
        }
    }
}
