using Microsoft.EntityFrameworkCore;
using MultiTenantSaaS.Application.Abstractions;
using MultiTenantSaaS.Application.Common;
using MultiTenantSaaS.Application.Features.Tenants;
using MultiTenantSaaS.Domain.Entities;
using MultiTenantSaaS.Domain.Enums;
using MultiTenantSaaS.Infrastructure.Identity;
using MultiTenantSaaS.Infrastructure.MultiTenancy;
using MultiTenantSaaS.Infrastructure.Persistence;
using Xunit;

namespace MultiTenantSaaS.UnitTests.Onboarding;

public sealed class TenantOnboardingTests : IDisposable
{
    private readonly TenantContext _tenantContext = new();
    private readonly ApplicationDbContext _db;
    private readonly TenantOnboardingService _sut;

    public TenantOnboardingTests()
    {
        _db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"onboarding-{Guid.NewGuid()}").Options,
            _tenantContext);

        _sut = new TenantOnboardingService(
            _db,
            new NoOpTransactionManager(),
            _tenantContext,
            new NoOpTenantStore(),
            new Pbkdf2PasswordHasher(),
            new FakeTokenGenerator());
    }

    public void Dispose() => _db.Dispose();

    private static RegisterTenantRequest ValidRequest(string slug = "acme-nou") => new()
    {
        OrganizationName = "Acme Nou SRL",
        Slug = slug,
        AdminEmail = "Admin@AcmeNou.RO",
        AdminPassword = "Parola-Sigura-123",
        AdminFullName = "Primul Administrator"
    };

    [Fact]
    public async Task Register_CreatesTenantAdminAndSeedData()
    {
        var result = await _sut.RegisterAsync(ValidRequest());

        Assert.Equal("acme-nou", result.Tenant.Slug);
        Assert.Equal(SubscriptionPlan.Free, result.Tenant.Plan);
        Assert.True(result.Tenant.IsActive);

        // Primul utilizator primește automat rolul de administrator de organizație,
        // altfel organizația ar fi creată fără nimeni care s-o administreze.
        Assert.Equal(UserRole.TenantAdmin, result.Admin.Role);
        Assert.Equal("admin@acmenou.ro", result.Admin.Email);

        Assert.Equal("GEN", result.Seeded.ProjectCode);
        Assert.NotEqual(Guid.Empty, result.Seeded.WelcomeTicketId);
        Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
    }

    [Fact]
    public async Task Register_StampsEverySeededRowWithTheNewTenant()
    {
        var result = await _sut.RegisterAsync(ValidRequest());
        var tenantId = result.Tenant.Id;

        // Citim ocolind filtrul, ca să vedem exact ce s-a scris în baza de date.
        Assert.Equal(tenantId, (await _db.Users.IgnoreQueryFilters().SingleAsync()).TenantId);
        Assert.Equal(tenantId, (await _db.Projects.IgnoreQueryFilters().SingleAsync()).TenantId);
        Assert.Equal(tenantId, (await _db.Tickets.IgnoreQueryFilters().SingleAsync()).TenantId);
    }

    [Fact]
    public async Task Register_LeavesNoAmbientTenantBehind()
    {
        await _sut.RegisterAsync(ValidRequest());

        // Scope-ul deschis intern trebuie închis: altfel restul cererii ar rula
        // în contextul organizației tocmai create.
        Assert.False(_tenantContext.IsResolved);
    }

    [Fact]
    public async Task Register_RestoresOuterTenantScope()
    {
        var outer = Guid.Parse("dddddddd-0000-0000-0000-000000000004");

        using (_tenantContext.BeginScope(outer, "extern"))
        {
            await _sut.RegisterAsync(ValidRequest());

            Assert.Equal(outer, _tenantContext.TenantId);
        }
    }

    [Fact]
    public async Task Register_WithDuplicateSlug_Conflicts()
    {
        await _sut.RegisterAsync(ValidRequest());

        var ex = await Assert.ThrowsAsync<ConflictException>(() => _sut.RegisterAsync(ValidRequest()));
        Assert.Contains("deja folosit", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Register_WithReservedSystemSlug_Conflicts()
    {
        await Assert.ThrowsAsync<ConflictException>(() =>
            _sut.RegisterAsync(ValidRequest(Tenant.SystemTenantSlug)));
    }

    [Fact]
    public async Task Register_WithInvalidSlug_ReturnsBadRequest()
    {
        // Validarea din DTO prinde asta înainte de serviciu, dar invariantul din domeniu
        // trebuie să reziste și pe căi care ocolesc controllerul.
        await Assert.ThrowsAsync<BadRequestException>(() => _sut.RegisterAsync(ValidRequest("nu valid!")));
    }

    [Fact]
    public async Task Register_TwoOrganizations_AreFullyIsolated()
    {
        var first = await _sut.RegisterAsync(ValidRequest("prima"));
        var second = await _sut.RegisterAsync(ValidRequest("adoua"));

        using (_tenantContext.BeginScope(first.Tenant.Id, "prima"))
        {
            Assert.Single(await _db.Projects.ToListAsync());
            Assert.Equal(first.Seeded.ProjectId, (await _db.Projects.SingleAsync()).Id);
            Assert.Empty(await _db.Users.Where(u => u.Id == second.Admin.Id).ToListAsync());
        }
    }

    [Fact]
    public async Task Register_WhenSecondStepFails_DoesNotCommit()
    {
        var transactions = new NoOpTransactionManager();
        var sut = new TenantOnboardingService(
            _db, transactions, _tenantContext, new NoOpTenantStore(),
            new Pbkdf2PasswordHasher(), new FakeTokenGenerator());

        // Nume gol: tenantul se salvează, apoi User.Create aruncă. Excepția iese din
        // scope-ul de tenant, tranzacția e eliberată fără commit, iar PostgreSQL derulează
        // înapoi tenantul deja scris - altfel ar rămâne o organizație fără administrator.
        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.RegisterAsync(ValidRequest("esec") with { AdminFullName = "   " }));

        Assert.False(transactions.LastTransaction!.Committed);
        Assert.False(_tenantContext.IsResolved);
    }

    // Provider-ul in-memory nu suportă tranzacții. Le înlocuim cu un no-op: aici testăm
    // logica de onboarding, nu comportamentul tranzacțional al PostgreSQL.
    private sealed class NoOpTransactionManager : ITransactionManager
    {
        public NoOpTransaction? LastTransaction { get; private set; }

        public Task<ITransaction> BeginAsync(CancellationToken cancellationToken = default)
        {
            LastTransaction = new NoOpTransaction();
            return Task.FromResult<ITransaction>(LastTransaction);
        }

        internal sealed class NoOpTransaction : ITransaction
        {
            public bool Committed { get; private set; }

            public Task CommitAsync(CancellationToken cancellationToken = default)
            {
                Committed = true;
                return Task.CompletedTask;
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private sealed class NoOpTenantStore : ITenantStore
    {
        public Task<TenantInfo?> FindAsync(string identifier, CancellationToken cancellationToken = default) =>
            Task.FromResult<TenantInfo?>(null);

        public void Invalidate(TenantInfo tenant)
        {
        }
    }

    private sealed class FakeTokenGenerator : IJwtTokenGenerator
    {
        public GeneratedToken Generate(User user, string tenantSlug) =>
            new($"token-{tenantSlug}", DateTime.UtcNow.AddHours(1));
    }
}
