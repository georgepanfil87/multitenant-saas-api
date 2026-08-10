using Microsoft.EntityFrameworkCore;
using MultiTenantSaaS.Application.Abstractions;
using MultiTenantSaaS.Application.Common;
using MultiTenantSaaS.Application.Features.Projects;
using MultiTenantSaaS.Application.Features.Tickets;
using MultiTenantSaaS.Domain.Entities;
using MultiTenantSaaS.Domain.Enums;
using MultiTenantSaaS.Infrastructure.MultiTenancy;
using MultiTenantSaaS.Infrastructure.Persistence;
using Xunit;

namespace MultiTenantSaaS.UnitTests.MultiTenancy;

/// <summary>
/// Verifică faptul central al Pasului 7: serviciile de CRUD nu conțin nicio verificare de
/// tenant, iar izolarea rezistă oricum - inclusiv când ID-urile sunt ghicite corect.
/// </summary>
public sealed class CrudIsolationTests : IDisposable
{
    private static readonly Guid AcmeId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid GlobexId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

    private readonly TenantContext _tenantContext = new();
    private readonly ApplicationDbContext _db;
    private readonly FakeCurrentUser _currentUser = new();
    private readonly ProjectService _projects;
    private readonly TicketService _tickets;

    public CrudIsolationTests()
    {
        _db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"crud-{Guid.NewGuid()}").Options,
            _tenantContext);

        _projects = new ProjectService(_db, _currentUser);
        _tickets = new TicketService(_db, _currentUser);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task List_ShowsOnlyCurrentTenantProjects()
    {
        await SeedAsync(AcmeId, "SUP");
        await SeedAsync(GlobexId, "SUP"); // același cod, altă organizație: permis

        using (Scope(AcmeId))
        {
            var page = await _projects.ListAsync(new PageRequest(), includeArchived: false);

            Assert.Single(page.Items);
            Assert.Equal(1, page.TotalCount);
        }
    }

    [Fact]
    public async Task TotalCount_DoesNotLeakOtherTenantsRowCount()
    {
        await SeedAsync(AcmeId, "AAA");
        await SeedAsync(GlobexId, "BBB");
        await SeedAsync(GlobexId, "CCC");

        using (Scope(AcmeId))
        {
            // COUNT-ul rulează peste același query filtrat. Cu filtrare manuală, exact aici
            // s-ar fi uitat condiția, iar totalul ar fi spus câte rânduri are toată platforma.
            Assert.Equal(1, (await _projects.ListAsync(new PageRequest(), false)).TotalCount);
        }
    }

    [Fact]
    public async Task Get_WithExactIdOfAnotherTenant_Returns404()
    {
        var (projectId, _) = await SeedAsync(GlobexId, "SUP");

        using (Scope(AcmeId))
        {
            // ID-ul e corect și există în platformă, dar pentru Acme e inexistent.
            await Assert.ThrowsAsync<NotFoundException>(() => _projects.GetAsync(projectId));
        }
    }

    [Fact]
    public async Task CreateTicket_InAnotherTenantsProject_Returns404()
    {
        var (globexProject, _) = await SeedAsync(GlobexId, "SUP");

        using (Scope(AcmeId))
        {
            var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
                _tickets.CreateAsync(new CreateTicketRequest
                {
                    ProjectId = globexProject,
                    Title = "Tichet strecurat"
                }));

            // Mesajul spune „nu există", nu „nu ai voie": un 403 ar confirma că ID-ul e real.
            Assert.Contains("nu există", ex.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task AssignTicket_ToUserOfAnotherTenant_Returns404()
    {
        var (_, acmeTicket) = await SeedAsync(AcmeId, "AAA");
        var globexUserId = await SeedUserAsync(GlobexId, "strain@globex.ro");

        using (Scope(AcmeId))
        {
            await Assert.ThrowsAsync<NotFoundException>(() =>
                _tickets.AssignAsync(acmeTicket, new AssignTicketRequest { AssignedToUserId = globexUserId }));
        }
    }

    [Fact]
    public async Task AssignTicket_ToUserOfSameTenant_Succeeds()
    {
        var (_, acmeTicket) = await SeedAsync(AcmeId, "AAA");
        var acmeUserId = await SeedUserAsync(AcmeId, "coleg@acme.ro");

        using (Scope(AcmeId))
        {
            var result = await _tickets.AssignAsync(
                acmeTicket, new AssignTicketRequest { AssignedToUserId = acmeUserId });

            Assert.Equal(acmeUserId, result.AssignedToUserId);
        }
    }

    [Fact]
    public async Task UpdateTicket_OfAnotherTenant_Returns404()
    {
        var (_, globexTicket) = await SeedAsync(GlobexId, "SUP");

        using (Scope(AcmeId))
        {
            await Assert.ThrowsAsync<NotFoundException>(() =>
                _tickets.UpdateAsync(globexTicket, new UpdateTicketRequest { Title = "Modificat de intrus" }));
        }
    }

    [Fact]
    public async Task DeleteProject_OfAnotherTenant_Returns404_AndLeavesItIntact()
    {
        var (globexProject, _) = await SeedAsync(GlobexId, "SUP");

        using (Scope(AcmeId))
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _projects.DeleteAsync(globexProject));
        }

        Assert.True(await _db.Projects.IgnoreQueryFilters().AnyAsync(p => p.Id == globexProject));
    }

    [Fact]
    public async Task CreateProject_WithCodeUsedByAnotherTenant_IsAllowed()
    {
        await SeedAsync(GlobexId, "SUP");

        using (Scope(AcmeId))
        {
            var created = await _projects.CreateAsync(new CreateProjectRequest { Name = "Suport", Code = "sup" });

            Assert.Equal("SUP", created.Code); // normalizat în domeniu
        }
    }

    [Fact]
    public async Task CreateProject_WithCodeUsedInSameTenant_Conflicts()
    {
        await SeedAsync(AcmeId, "SUP");

        using (Scope(AcmeId))
        {
            await Assert.ThrowsAsync<ConflictException>(() =>
                _projects.CreateAsync(new CreateProjectRequest { Name = "Alt suport", Code = "SUP" }));
        }
    }

    [Fact]
    public async Task ChangeStatus_WithForbiddenTransition_ReturnsBadRequest()
    {
        var (_, ticketId) = await SeedAsync(AcmeId, "AAA");

        using (Scope(AcmeId))
        {
            // Open -> Closed direct nu e permis de mașina de stări din entitate.
            await Assert.ThrowsAsync<BadRequestException>(() =>
                _tickets.ChangeStatusAsync(ticketId, new ChangeTicketStatusRequest
                {
                    Status = TicketStatus.Closed
                }));
        }
    }

    [Fact]
    public async Task CreateTicket_InArchivedProject_ReturnsBadRequest()
    {
        var (projectId, _) = await SeedAsync(AcmeId, "AAA");

        using (Scope(AcmeId))
        {
            await _projects.SetArchivedAsync(projectId, archived: true);

            await Assert.ThrowsAsync<BadRequestException>(() =>
                _tickets.CreateAsync(new CreateTicketRequest { ProjectId = projectId, Title = "Nu ar trebui" }));
        }
    }

    [Fact]
    public async Task Pagination_RespectsPageSizeAndReportsTotal()
    {
        var (projectId, _) = await SeedAsync(AcmeId, "AAA");

        using (Scope(AcmeId))
        {
            for (var i = 0; i < 7; i++)
            {
                await _tickets.CreateAsync(new CreateTicketRequest { ProjectId = projectId, Title = $"Tichet {i}" });
            }

            var page = await _tickets.ListAsync(new TicketFilter(), new PageRequest { Page = 2, PageSize = 3 });

            Assert.Equal(3, page.Items.Count);
            Assert.Equal(8, page.TotalCount); // 7 + tichetul creat la seed
            Assert.Equal(3, page.TotalPages);
            Assert.True(page.HasNextPage);
        }
    }

    private IDisposable Scope(Guid tenantId)
    {
        _currentUser.UserId = Guid.NewGuid();
        return _tenantContext.BeginScope(tenantId, tenantId == AcmeId ? "acme" : "globex");
    }

    private async Task<(Guid ProjectId, Guid TicketId)> SeedAsync(Guid tenantId, string code)
    {
        using (_tenantContext.BeginScope(tenantId))
        {
            var project = Project.Create($"Proiect {code}", code, Guid.NewGuid());
            var ticket = Ticket.Create(project.Id, $"Tichet {code}", Guid.NewGuid());

            _db.Projects.Add(project);
            _db.Tickets.Add(ticket);
            await _db.SaveChangesAsync();

            _db.Entry(project).State = EntityState.Detached;
            _db.Entry(ticket).State = EntityState.Detached;

            return (project.Id, ticket.Id);
        }
    }

    private async Task<Guid> SeedUserAsync(Guid tenantId, string email)
    {
        using (_tenantContext.BeginScope(tenantId))
        {
            var user = User.Create(email, "hash", "Utilizator");
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
            _db.Entry(user).State = EntityState.Detached;
            return user.Id;
        }
    }

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public Guid? UserId { get; set; } = Guid.NewGuid();

        public string? Email => "test@exemplu.ro";

        public UserRole? Role => UserRole.TenantAdmin;

        public bool IsAuthenticated => true;
    }
}
