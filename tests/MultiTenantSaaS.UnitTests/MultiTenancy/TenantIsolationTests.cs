using Microsoft.EntityFrameworkCore;
using MultiTenantSaaS.Domain.Entities;
using MultiTenantSaaS.Infrastructure.MultiTenancy;
using MultiTenantSaaS.Infrastructure.Persistence;
using Xunit;

namespace MultiTenantSaaS.UnitTests.MultiTenancy;

/// <summary>
/// The critical isolation scenarios. If any of these fails, the product leaks data between
/// clients.
/// </summary>
public sealed class TenantIsolationTests : IDisposable
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly TenantContext _tenantContext = new();
    private readonly ApplicationDbContext _db;

    public TenantIsolationTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"isolation-{Guid.NewGuid()}")
            .Options;

        _db = new ApplicationDbContext(options, _tenantContext);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Query_ReturnsOnlyCurrentTenantRows()
    {
        await SeedProjectAsync(TenantA, "Proiect A", "AAA");
        await SeedProjectAsync(TenantB, "Proiect B", "BBB");

        using (_tenantContext.BeginScope(TenantA))
        {
            var projects = await _db.Projects.ToListAsync();

            Assert.Single(projects);
            Assert.Equal("Proiect A", projects[0].Name);
        }
    }

    [Fact]
    public async Task Query_ByExplicitId_DoesNotLeakOtherTenantRow()
    {
        var idOfB = await SeedProjectAsync(TenantB, "Proiect B", "BBB");

        using (_tenantContext.BeginScope(TenantA))
        {
            // Even with the exact id of another tenant's row the result is null: the filter
            // applies before the key lookup.
            var stolen = await _db.Projects.FirstOrDefaultAsync(p => p.Id == idOfB);

            Assert.Null(stolen);
        }
    }

    [Fact]
    public async Task Insert_StampsTenantIdAutomatically()
    {
        using (_tenantContext.BeginScope(TenantA))
        {
            var project = Project.Create("Proiect nou", "NEW", Guid.NewGuid());
            Assert.Equal(Guid.Empty, project.TenantId);

            _db.Projects.Add(project);
            await _db.SaveChangesAsync();

            Assert.Equal(TenantA, project.TenantId);
        }
    }

    [Fact]
    public async Task Insert_WithoutResolvedTenant_Throws()
    {
        _db.Projects.Add(Project.Create("Fără tenant", "NOP", Guid.NewGuid()));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _db.SaveChangesAsync());

        Assert.Contains("no tenant resolved", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Insert_OfRowBelongingToAnotherTenant_Throws()
    {
        Project project;
        using (_tenantContext.BeginScope(TenantA))
        {
            project = Project.Create("Proiect A", "AAA", Guid.NewGuid());
            _db.Projects.Add(project);
            await _db.SaveChangesAsync();
        }

        _db.Entry(project).State = EntityState.Detached;

        using (_tenantContext.BeginScope(TenantB))
        {
            _db.Entry(project).State = EntityState.Added;

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _db.SaveChangesAsync());

            Assert.Contains("the current context is", ex.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Update_ThatMovesRowToAnotherTenant_IsBlockedBySaveChangesGuard()
    {
        Guid id;
        using (_tenantContext.BeginScope(TenantA))
        {
            var user = User.Create("george@acme.ro", "hash", "George Panfil");
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
            id = user.Id;
            _db.Entry(user).State = EntityState.Detached;
        }

        using (_tenantContext.BeginScope(TenantA))
        {
            var user = await _db.Users.SingleAsync(u => u.Id == id);

            // Bypass the private setter exactly as a bug or an insider attack would.
            _db.Entry(user).Property(nameof(User.TenantId)).CurrentValue = TenantB;

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _db.SaveChangesAsync());

            Assert.Contains("cannot be changed", ex.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Update_ThatMovesProjectToAnotherTenant_IsBlockedEarlierByEfCore()
    {
        var id = await SeedProjectAsync(TenantA, "Proiect A", "AAA");

        using (_tenantContext.BeginScope(TenantA))
        {
            var project = await _db.Projects.SingleAsync(p => p.Id == id);

            // On Project, TenantId is part of the (TenantId, Id) alternate key targeted by the
            // composite foreign key from Tickets, so EF Core refuses the change at assignment
            // time, before our SaveChanges guard runs. A second layer, for free.
            var ex = Assert.Throws<InvalidOperationException>(() =>
                _db.Entry(project).Property(nameof(Project.TenantId)).CurrentValue = TenantB);

            Assert.Contains("part of a key", ex.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task IgnoreQueryFilters_IsTheOnlyWayToSeeEverything()
    {
        await SeedProjectAsync(TenantA, "Proiect A", "AAA");
        await SeedProjectAsync(TenantB, "Proiect B", "BBB");

        using (_tenantContext.BeginScope(TenantA))
        {
            Assert.Single(await _db.Projects.ToListAsync());
            Assert.Equal(2, await _db.Projects.IgnoreQueryFilters().CountAsync());
        }
    }

    [Fact]
    public void NestedScopes_RestorePreviousTenantOnExit()
    {
        using (_tenantContext.BeginScope(TenantA))
        {
            using (_tenantContext.BeginScope(TenantB))
            {
                Assert.Equal(TenantB, _tenantContext.TenantId);
            }

            Assert.Equal(TenantA, _tenantContext.TenantId);
        }

        Assert.False(_tenantContext.IsResolved);
    }

    private async Task<Guid> SeedProjectAsync(Guid tenantId, string name, string code)
    {
        using (_tenantContext.BeginScope(tenantId))
        {
            var project = Project.Create(name, code, Guid.NewGuid());
            _db.Projects.Add(project);
            await _db.SaveChangesAsync();
            _db.Entry(project).State = EntityState.Detached;
            return project.Id;
        }
    }
}
