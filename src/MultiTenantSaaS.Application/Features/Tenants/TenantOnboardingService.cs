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

/// <summary>Creates a new organization together with its first administrator and seed data.</summary>
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
            throw new ConflictException($"The slug \"{slug}\" is reserved by the platform.");
        }

        if (await db.Tenants.AnyAsync(t => t.Slug == slug, cancellationToken))
        {
            throw new ConflictException($"The slug \"{slug}\" is already taken by another organization.");
        }

        // Two consecutive SaveChanges: the first creates the tenant, the second writes inside it.
        // The transaction makes them indivisible; otherwise a failure in step two would leave an
        // organization with no users: unreachable, yet holding its slug.
        await using var transaction = await transactions.BeginAsync(cancellationToken);

        Tenant tenant;
        try
        {
            // Tenant is not an ITenantEntity, so it can be saved without a tenant context.
            tenant = Tenant.Create(request.OrganizationName, slug);
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (ArgumentException ex)
        {
            // Domain invariants (bad slug, empty name) are client errors.
            throw new BadRequestException(ex.Message);
        }
        catch (DbUpdateException)
        {
            // The check above can be raced by concurrent requests; the unique index is the
            // final arbiter.
            throw new ConflictException($"The slug \"{slug}\" is already taken by another organization.");
        }

        User admin;
        Project project;
        Ticket welcomeTicket;

        // Enter the newly created organization's context. The scope nests over the request's own,
        // and on exit the previous context is restored exactly.
        using (tenantContext.BeginScope(tenant.Id, tenant.Slug))
        {
            admin = User.Create(
                request.AdminEmail,
                passwordHasher.Hash(request.AdminPassword),
                request.AdminFullName,
                UserRole.TenantAdmin);

            project = Project.Create(DefaultProjectName, DefaultProjectCode, admin.Id,
                "Created automatically when the organization was registered.");

            welcomeTicket = Ticket.Create(
                project.Id,
                "Welcome!",
                admin.Id,
                "This is your first ticket. You can edit it, assign it or close it.",
                TicketPriority.Low);

            db.Users.Add(admin);
            db.Projects.Add(project);
            db.Tickets.Add(welcomeTicket);

            // One SaveChanges for all three: the DbContext stamps each with the scoped tenant.
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
        // The Tenants table is not tenant-scoped, so there is no query filter to bypass.
        // Access is restricted to GlobalAdmin by policy on the controller.
        var tenants = await db.Tenants
            .AsNoTracking()
            .OrderBy(t => t.Slug)
            .ToListAsync(cancellationToken);

        return tenants.ConvertAll(ToSummary);
    }

    private static TenantSummary ToSummary(Tenant tenant) =>
        new(tenant.Id, tenant.Slug, tenant.Name, tenant.Plan, tenant.IsActive, tenant.CreatedAtUtc);
}
