using Microsoft.EntityFrameworkCore;
using MultiTenantSaaS.Application.Abstractions;
using MultiTenantSaaS.Application.Common;
using MultiTenantSaaS.Domain.Entities;

namespace MultiTenantSaaS.Application.Features.Projects;

public interface IProjectService
{
    Task<PagedResult<ProjectResponse>> ListAsync(
        PageRequest page, bool includeArchived, CancellationToken cancellationToken = default);

    Task<ProjectResponse> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ProjectResponse> CreateAsync(CreateProjectRequest request, CancellationToken cancellationToken = default);

    Task<ProjectResponse> UpdateAsync(
        Guid id, UpdateProjectRequest request, CancellationToken cancellationToken = default);

    Task<ProjectResponse> SetArchivedAsync(Guid id, bool archived, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Project CRUD. Note that no tenant condition appears anywhere below: isolation comes from
/// the global query filter on reads and from TenantId stamping on writes. Another
/// organization's project is not "forbidden", it is simply not found, hence 404 over 403.
/// </summary>
public sealed class ProjectService(IApplicationDbContext db, ICurrentUser currentUser) : IProjectService
{
    public async Task<PagedResult<ProjectResponse>> ListAsync(
        PageRequest page,
        bool includeArchived,
        CancellationToken cancellationToken = default)
    {
        var query = db.Projects.AsNoTracking();

        if (!includeArchived)
        {
            query = query.Where(p => !p.IsArchived);
        }

        return await query
            .OrderByDescending(p => p.CreatedAtUtc)
            .Select(p => new ProjectResponse(
                p.Id, p.Name, p.Code, p.Description, p.IsArchived, p.CreatedByUserId,
                // The subquery is tenant-filtered too, so the count cannot include
                // another organization's tickets.
                db.Tickets.Count(t => t.ProjectId == p.Id),
                p.CreatedAtUtc))
            .ToPagedResultAsync(page, cancellationToken);
    }

    public async Task<ProjectResponse> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var project = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Project {id} does not exist.");

        var ticketCount = await db.Tickets.CountAsync(t => t.ProjectId == id, cancellationToken);

        return ToResponse(project, ticketCount);
    }

    public async Task<ProjectResponse> CreateAsync(
        CreateProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var code = request.Code.Trim().ToUpperInvariant();

        // Code uniqueness is per organization: this check runs filtered, so a code used by
        // another client does not block anything here.
        if (await db.Projects.AnyAsync(p => p.Code == code, cancellationToken))
        {
            throw new ConflictException($"A project with the code {code} already exists in this organization.");
        }

        var project = Create(() => Project.Create(request.Name, request.Code, RequireUserId(), request.Description));

        db.Projects.Add(project);
        await db.SaveChangesAsync(cancellationToken);

        return ToResponse(project, ticketCount: 0);
    }

    public async Task<ProjectResponse> UpdateAsync(
        Guid id,
        UpdateProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Project {id} does not exist.");

        Create(() =>
        {
            project.Update(request.Name, request.Description);
            return project;
        });

        await db.SaveChangesAsync(cancellationToken);

        return ToResponse(project, await db.Tickets.CountAsync(t => t.ProjectId == id, cancellationToken));
    }

    public async Task<ProjectResponse> SetArchivedAsync(
        Guid id,
        bool archived,
        CancellationToken cancellationToken = default)
    {
        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Project {id} does not exist.");

        if (archived)
        {
            project.Archive();
        }
        else
        {
            project.Restore();
        }

        await db.SaveChangesAsync(cancellationToken);

        return ToResponse(project, await db.Tickets.CountAsync(t => t.ProjectId == id, cancellationToken));
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Project {id} does not exist.");

        // Tickets cascade away through the composite foreign key.
        db.Projects.Remove(project);
        await db.SaveChangesAsync(cancellationToken);
    }

    private Guid RequireUserId() =>
        currentUser.UserId ?? throw new AuthenticationFailedException("The request is not authenticated.");

    // Domain invariants throw ArgumentException; for the client those are 400s, not 500s.
    // Translated here rather than in the controller so every entry path is covered.
    private static T Create<T>(Func<T> action)
    {
        try
        {
            return action();
        }
        catch (ArgumentException ex)
        {
            throw new BadRequestException(ex.Message);
        }
    }

    private static ProjectResponse ToResponse(Project project, int ticketCount) =>
        new(project.Id, project.Name, project.Code, project.Description, project.IsArchived,
            project.CreatedByUserId, ticketCount, project.CreatedAtUtc);
}
