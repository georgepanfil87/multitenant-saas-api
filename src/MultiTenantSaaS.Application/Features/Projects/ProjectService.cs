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
/// CRUD pentru proiecte.
/// </summary>
/// <remarks>
/// Observă că nicăieri în acest fișier nu apare <c>TenantId</c>. Izolarea vine integral din
/// global query filter (citiri) și din ștampilarea de la <c>SaveChanges</c> (scrieri).
/// Un proiect al altei organizații nu e „interzis", ci pur și simplu <b>inexistent</b>
/// pentru query-urile de aici - de unde și 404 în loc de 403.
/// </remarks>
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
                // Subquery-ul e la rândul lui filtrat pe tenant: numărul de tichete
                // nu poate include tichetele altei organizații.
                db.Tickets.Count(t => t.ProjectId == p.Id),
                p.CreatedAtUtc))
            .ToPagedResultAsync(page, cancellationToken);
    }

    public async Task<ProjectResponse> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var project = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Proiectul {id} nu există.");

        var ticketCount = await db.Tickets.CountAsync(t => t.ProjectId == id, cancellationToken);

        return ToResponse(project, ticketCount);
    }

    public async Task<ProjectResponse> CreateAsync(
        CreateProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var code = request.Code.Trim().ToUpperInvariant();

        // Unicitatea codului e per organizație: verificarea rulează filtrat, deci un cod
        // folosit de alt client nu blochează nimic aici.
        if (await db.Projects.AnyAsync(p => p.Code == code, cancellationToken))
        {
            throw new ConflictException($"Există deja un proiect cu codul {code} în această organizație.");
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
            ?? throw new NotFoundException($"Proiectul {id} nu există.");

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
            ?? throw new NotFoundException($"Proiectul {id} nu există.");

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
            ?? throw new NotFoundException($"Proiectul {id} nu există.");

        // Tichetele se șterg în cascadă, prin FK-ul compus definit la Pasul 3.
        db.Projects.Remove(project);
        await db.SaveChangesAsync(cancellationToken);
    }

    private Guid RequireUserId() =>
        currentUser.UserId ?? throw new AuthenticationFailedException("Cererea nu este autentificată.");

    // Invarianții din domeniu aruncă ArgumentException; pentru client sunt erori 400,
    // nu 500. Traducerea se face aici, nu în controller, ca să fie valabilă pe orice cale.
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
