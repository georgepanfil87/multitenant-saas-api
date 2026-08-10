using Microsoft.EntityFrameworkCore;
using MultiTenantSaaS.Application.Abstractions;
using MultiTenantSaaS.Application.Common;
using MultiTenantSaaS.Application.Features.Authentication;
using MultiTenantSaaS.Domain.Entities;
using MultiTenantSaaS.Domain.Enums;

namespace MultiTenantSaaS.Application.Features.Tenants;

public interface ITenantOnboardingService
{
    Task<TenantRegistrationResponse> RegisterAsync(
        RegisterTenantRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TenantSummary>> ListAllAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Creează o organizație nouă împreună cu primul ei administrator și cu datele inițiale.
/// </summary>
public sealed class TenantOnboardingService(
    IApplicationDbContext db,
    ITransactionManager transactions,
    ITenantContext tenantContext,
    ITenantStore tenantStore,
    IPasswordHasher passwordHasher,
    IJwtTokenGenerator tokenGenerator) : ITenantOnboardingService
{
    private const string DefaultProjectName = "General";
    private const string DefaultProjectCode = "GEN";

    public async Task<TenantRegistrationResponse> RegisterAsync(
        RegisterTenantRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var slug = request.Slug.Trim().ToLowerInvariant();

        if (string.Equals(slug, Tenant.SystemTenantSlug, StringComparison.Ordinal))
        {
            throw new ConflictException($"Slug-ul „{slug}\" este rezervat platformei.");
        }

        if (await db.Tenants.AnyAsync(t => t.Slug == slug, cancellationToken))
        {
            throw new ConflictException($"Slug-ul „{slug}\" este deja folosit de altă organizație.");
        }

        // Două SaveChanges succesive: primul creează tenantul, al doilea scrie în interiorul lui.
        // Tranzacția le face indivizibile - altfel o eroare la pasul doi ar lăsa o organizație
        // fără niciun utilizator, adică imposibil de accesat și imposibil de reînregistrat.
        await using var transaction = await transactions.BeginAsync(cancellationToken);

        Tenant tenant;
        try
        {
            // Tenant nu implementează ITenantEntity, deci se poate salva fără context de tenant.
            tenant = Tenant.Create(request.OrganizationName, slug);
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (ArgumentException ex)
        {
            // Invarianții din domeniu (slug invalid, nume gol) sunt erori ale clientului.
            throw new BadRequestException(ex.Message);
        }
        catch (DbUpdateException)
        {
            // Verificarea de mai sus poate fi depășită de două cereri simultane; indexul unic
            // din PostgreSQL este arbitrul final.
            throw new ConflictException($"Slug-ul „{slug}\" este deja folosit de altă organizație.");
        }

        User admin;
        Project project;
        Ticket welcomeTicket;

        // Intrăm în contextul organizației abia create. Scope-ul e imbricat peste cel al cererii,
        // iar la ieșire contextul revine exact la ce era înainte.
        using (tenantContext.BeginScope(tenant.Id, tenant.Slug))
        {
            admin = User.Create(
                request.AdminEmail,
                passwordHasher.Hash(request.AdminPassword),
                request.AdminFullName,
                UserRole.TenantAdmin);

            project = Project.Create(DefaultProjectName, DefaultProjectCode, admin.Id,
                "Proiect creat automat la înregistrarea organizației.");

            welcomeTicket = Ticket.Create(
                project.Id,
                "Bine ai venit!",
                admin.Id,
                "Acesta este primul tău tichet. Îl poți edita, aloca sau închide.",
                TicketPriority.Low);

            db.Users.Add(admin);
            db.Projects.Add(project);
            db.Tickets.Add(welcomeTicket);

            // Un singur SaveChanges pentru toate trei: DbContext-ul le ștampilează pe toate
            // cu tenantul din scope-ul curent.
            await db.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        var info = new TenantInfo(tenant.Id, tenant.Slug, tenant.Name, tenant.Plan, tenant.IsActive,
            tenant.RequestsPerMinuteOverride);

        tenantStore.Invalidate(info);

        var token = tokenGenerator.Generate(admin, tenant.Slug);

        return new TenantRegistrationResponse(
            ToSummary(tenant),
            new UserResponse(admin.Id, admin.Email, admin.FullName, admin.Role, admin.IsActive, admin.LastLoginAtUtc),
            token.AccessToken,
            "Bearer",
            token.ExpiresAtUtc,
            new SeededData(project.Id, project.Code, welcomeTicket.Id));
    }

    public async Task<IReadOnlyList<TenantSummary>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        // Tabela Tenants nu e tenant-scoped, deci nu are query filter de ocolit.
        // Restricția la GlobalAdmin se face prin policy, pe controller.
        var tenants = await db.Tenants
            .AsNoTracking()
            .OrderBy(t => t.Slug)
            .ToListAsync(cancellationToken);

        return tenants.ConvertAll(ToSummary);
    }

    private static TenantSummary ToSummary(Tenant tenant) =>
        new(tenant.Id, tenant.Slug, tenant.Name, tenant.Plan, tenant.IsActive, tenant.CreatedAtUtc);
}
