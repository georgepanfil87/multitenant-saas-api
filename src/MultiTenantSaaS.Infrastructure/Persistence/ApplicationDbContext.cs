using System.Reflection;
using Microsoft.EntityFrameworkCore;
using MultiTenantSaaS.Application.Abstractions;
using MultiTenantSaaS.Domain.Common;
using MultiTenantSaaS.Domain.Entities;
using MultiTenantSaaS.Infrastructure.Persistence.Converters;

namespace MultiTenantSaaS.Infrastructure.Persistence;

/// <summary>
/// Contextul EF Core al aplicației. Aplică automat izolarea pe tenant: filtrează la citire
/// și ștampilează <c>TenantId</c> la scriere, pentru orice entitate <see cref="ITenantEntity"/>.
/// </summary>
public sealed class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options,
    ITenantContext tenantContext) : DbContext(options)
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
    /// Tenantul folosit de query filter. <see cref="Guid.Empty"/> când nu s-a rezolvat niciunul,
    /// ceea ce nu se potrivește cu niciun rând: sistemul eșuează închis, nu deschis.
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

        // Filtrul nu se scrie manual pe fiecare entitate. Se descoperă prin reflection tot
        // ce implementează ITenantEntity, deci o entitate nouă nu poate fi uitată nefiltrată.
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

    // Expresia referă CurrentTenantId, membru al contextului: EF Core îl transformă într-un
    // parametru SQL reevaluat la fiecare execuție, nu într-o constantă înghețată în model.
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
                            $"Nu se poate salva {entry.Entity.GetType().Name}: " +
                            "niciun tenant rezolvat pentru operațiunea curentă.");

                    var assignedTenantId = (Guid)tenantProperty.CurrentValue!;
                    if (assignedTenantId == Guid.Empty)
                    {
                        tenantProperty.CurrentValue = currentTenantId;
                    }
                    else if (assignedTenantId != currentTenantId)
                    {
                        throw new InvalidOperationException(
                            $"Se încearcă scrierea unui {entry.Entity.GetType().Name} în tenantul " +
                            $"{assignedTenantId}, dar contextul curent este {currentTenantId}.");
                    }

                    break;

                case EntityState.Modified:
                case EntityState.Deleted:
                    // Prinde încercarea de a muta un rând dintr-un tenant în altul. Rândurile
                    // ajung aici filtrate, deci ar trebui să fie imposibil - de asta e InvalidOperation,
                    // nu un mesaj de validare: dacă se declanșează, ceva mai grav e stricat.
                    if (!Equals(tenantProperty.OriginalValue, tenantProperty.CurrentValue))
                    {
                        throw new InvalidOperationException(
                            $"TenantId nu poate fi modificat pe {entry.Entity.GetType().Name} " +
                            $"({tenantProperty.OriginalValue} -> {tenantProperty.CurrentValue}).");
                    }

                    break;
            }
        }
    }
}
