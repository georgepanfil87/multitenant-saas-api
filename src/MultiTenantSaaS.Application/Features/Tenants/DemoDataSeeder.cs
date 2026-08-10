using Microsoft.EntityFrameworkCore;
using MultiTenantSaaS.Application.Abstractions;
using MultiTenantSaaS.Domain.Entities;
using MultiTenantSaaS.Domain.Enums;

namespace MultiTenantSaaS.Application.Features.Tenants;

public interface IDemoDataSeeder
{
    Task SeedAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Populates three demo organizations so Swagger has something to exercise, isolation
/// included. Runs only when explicitly enabled and only on an otherwise empty database.
/// </summary>
public sealed class DemoDataSeeder(
    IApplicationDbContext db,
    ITenantContext tenantContext,
    IPasswordHasher passwordHasher) : IDemoDataSeeder
{
    /// <summary>Shared password for every demo account. Documented in the Swagger description.</summary>
    public const string DemoPassword = "Demo123!parola";

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        // Idempotent: any organization other than the system one means the database is already
        // populated, with demo or real data. Either way, leave it alone.
        var alreadyPopulated = await db.Tenants
            .AnyAsync(t => t.Id != Tenant.SystemTenantId, cancellationToken);

        if (alreadyPopulated)
        {
            return;
        }

        await SeedPlatformAdminAsync(cancellationToken);

        await SeedAcmeAsync(cancellationToken);
        await SeedGlobexAsync(cancellationToken);
        await SeedInitechAsync(cancellationToken);
    }

    private async Task SeedPlatformAdminAsync(CancellationToken cancellationToken)
    {
        // The platform admin lives in the system tenant. Created here rather than through the
        // API, because granting GlobalAdmin via an endpoint is deliberately impossible.
        using (tenantContext.BeginScope(Tenant.SystemTenantId, Tenant.SystemTenantSlug))
        {
            db.Users.Add(User.Create(
                "platform@exemplu.ro", passwordHasher.Hash(DemoPassword),
                "Administrator Platformă", UserRole.GlobalAdmin));

            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task SeedAcmeAsync(CancellationToken cancellationToken)
    {
        var tenant = Tenant.Create("Acme Corporation", "acme", SubscriptionPlan.Pro);
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync(cancellationToken);

        using (tenantContext.BeginScope(tenant.Id, tenant.Slug))
        {
            var admin = AddUser("admin@acme.ro", "Andrei Popescu", UserRole.TenantAdmin);
            var maria = AddUser("maria@acme.ro", "Maria Ionescu", UserRole.Member);
            AddUser("radu@acme.ro", "Radu Georgescu", UserRole.Member);

            var support = AddProject("Suport Clienți", "SUP", admin.Id, "Cereri venite de la clienți.");
            var platform = AddProject("Platformă", "PLT", admin.Id, "Dezvoltare produs.");

            AddTicket(support, "Nu pot reseta parola", admin.Id, TicketPriority.High,
                "Utilizatorul nu primește emailul de resetare.", maria.Id, TicketStatus.InProgress);

            AddTicket(support, "Factura lunii martie e greșită", admin.Id, TicketPriority.Critical,
                "Suma facturată nu corespunde planului.", maria.Id, TicketStatus.Resolved);

            AddTicket(support, "Cerere export CSV", maria.Id, TicketPriority.Low,
                "Clientul vrea export de tichete în CSV.");

            AddTicket(platform, "Migrare la .NET 8", admin.Id, TicketPriority.Medium,
                "Actualizare de framework pe toate serviciile.", admin.Id, TicketStatus.InProgress);

            AddTicket(platform, "Adaugă autentificare cu doi factori", admin.Id, TicketPriority.High,
                "TOTP pentru conturile de administrator.");

            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task SeedGlobexAsync(CancellationToken cancellationToken)
    {
        var tenant = Tenant.Create("Globex SRL", "globex");
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync(cancellationToken);

        using (tenantContext.BeginScope(tenant.Id, tenant.Slug))
        {
            var admin = AddUser("admin@globex.ro", "Elena Marin", UserRole.TenantAdmin);
            var ion = AddUser("ion@globex.ro", "Ion Dumitrescu", UserRole.Member);

            // Same project code as Acme: allowed, because uniqueness is (TenantId, Code).
            var support = AddProject("Suport", "SUP", admin.Id, "Coada de suport Globex.");

            AddTicket(support, "Integrare cu ERP-ul intern", admin.Id, TicketPriority.Medium,
                "Sincronizare bidirecțională de comenzi.", ion.Id, TicketStatus.InProgress);

            AddTicket(support, "Raportul zilnic ajunge cu întârziere", ion.Id, TicketPriority.Low);

            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task SeedInitechAsync(CancellationToken cancellationToken)
    {
        var tenant = Tenant.Create("Initech", "initech", SubscriptionPlan.Enterprise);
        tenant.SetRateLimitOverride(2000);
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync(cancellationToken);

        using (tenantContext.BeginScope(tenant.Id, tenant.Slug))
        {
            var admin = AddUser("admin@initech.ro", "Ana Fondator", UserRole.TenantAdmin);
            var project = AddProject("Operațiuni", "OPS", admin.Id);

            AddTicket(project, "Audit de securitate trimestrial", admin.Id, TicketPriority.High);
            AddTicket(project, "Actualizare politici de acces", admin.Id, TicketPriority.Medium);

            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private User AddUser(string email, string fullName, UserRole role)
    {
        var user = User.Create(email, passwordHasher.Hash(DemoPassword), fullName, role);
        db.Users.Add(user);
        return user;
    }

    private Project AddProject(string name, string code, Guid createdBy, string? description = null)
    {
        var project = Project.Create(name, code, createdBy, description);
        db.Projects.Add(project);
        return project;
    }

    private void AddTicket(
        Project project,
        string title,
        Guid createdBy,
        TicketPriority priority,
        string? description = null,
        Guid? assignedTo = null,
        TicketStatus status = TicketStatus.Open)
    {
        var ticket = Ticket.Create(project.Id, title, createdBy, description, priority);

        if (assignedTo is not null)
        {
            ticket.AssignTo(assignedTo);
        }

        if (status != TicketStatus.Open)
        {
            // Walk through the intermediate states: the entity's state machine forbids jumps.
            ticket.ChangeStatus(TicketStatus.InProgress);

            if (status == TicketStatus.Resolved)
            {
                ticket.ChangeStatus(TicketStatus.Resolved);
            }
        }

        db.Tickets.Add(ticket);
    }
}
