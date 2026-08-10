using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using MultiTenantSaaS.Application.Abstractions;
using MultiTenantSaaS.Application.Common;
using MultiTenantSaaS.Domain.Entities;

namespace MultiTenantSaaS.Application.Features.Tickets;

public interface ITicketService
{
    Task<PagedResult<TicketResponse>> ListAsync(
        TicketFilter filter, PageRequest page, CancellationToken cancellationToken = default);

    Task<TicketResponse> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<TicketResponse> CreateAsync(CreateTicketRequest request, CancellationToken cancellationToken = default);

    Task<TicketResponse> UpdateAsync(
        Guid id, UpdateTicketRequest request, CancellationToken cancellationToken = default);

    Task<TicketResponse> ChangeStatusAsync(
        Guid id, ChangeTicketStatusRequest request, CancellationToken cancellationToken = default);

    Task<TicketResponse> AssignAsync(
        Guid id, AssignTicketRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// CRUD pentru tichete, entitatea centrală a produsului.
/// </summary>
/// <remarks>
/// Ca și la proiecte, nicio condiție pe <c>TenantId</c> nu apare în acest fișier.
/// Locurile interesante sunt cele unde un ID vine din corpul cererii - proiect și
/// responsabil: acolo verificarea de existență rulează filtrat, deci o referință
/// către altă organizație eșuează ca „inexistent", nu ca „interzis".
/// </remarks>
public sealed class TicketService(IApplicationDbContext db, ICurrentUser currentUser) : ITicketService
{
    public async Task<PagedResult<TicketResponse>> ListAsync(
        TicketFilter filter,
        PageRequest page,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var query = db.Tickets.AsNoTracking();

        if (filter.ProjectId is { } projectId)
        {
            query = query.Where(t => t.ProjectId == projectId);
        }

        if (filter.Status is { } status)
        {
            query = query.Where(t => t.Status == status);
        }

        if (filter.Priority is { } priority)
        {
            query = query.Where(t => t.Priority == priority);
        }

        if (filter.AssignedToUserId is { } assignee)
        {
            query = query.Where(t => t.AssignedToUserId == assignee);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            // Escapăm wildcard-urile LIKE: altfel o căutare după „100%" ar returna tot.
            var search = filter.Search.Trim().ToLowerInvariant()
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("%", "\\%", StringComparison.Ordinal)
                .Replace("_", "\\_", StringComparison.Ordinal);

            // Like (nu ILike): ILike e specific Npgsql și ar fi adus furnizorul de bază de date
            // în stratul Application. Insensibilitatea la majuscule o obținem cu ToLower.
            query = query.Where(t => EF.Functions.Like(t.Title.ToLower(), $"%{search}%", "\\"));
        }

        return await query
            .OrderByDescending(t => t.CreatedAtUtc)
            .Select(Projection)
            .ToPagedResultAsync(page, cancellationToken);
    }

    public async Task<TicketResponse> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await db.Tickets.AsNoTracking()
                   .Where(t => t.Id == id)
                   .Select(Projection)
                   .FirstOrDefaultAsync(cancellationToken)
               ?? throw new NotFoundException($"Tichetul {id} nu există.");
    }

    public async Task<TicketResponse> CreateAsync(
        CreateTicketRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Punctul critic al pasului: ProjectId vine de la client. Query-ul e filtrat pe
        // tenant, deci proiectul altei organizații nu se găsește și primim 404 - fără
        // nicio verificare scrisă de mână. Un 403 ar fi fost mai rău: ar fi confirmat
        // atacatorului că ID-ul respectiv există undeva în platformă.
        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == request.ProjectId, cancellationToken)
            ?? throw new NotFoundException($"Proiectul {request.ProjectId} nu există.");

        if (project.IsArchived)
        {
            throw new BadRequestException("Nu se pot adăuga tichete într-un proiect arhivat.");
        }

        var ticket = Guard(() => Ticket.Create(
            project.Id, request.Title, RequireUserId(), request.Description, request.Priority, request.DueDateUtc));

        db.Tickets.Add(ticket);
        await db.SaveChangesAsync(cancellationToken);

        return ToResponse(ticket, project.Code);
    }

    public async Task<TicketResponse> UpdateAsync(
        Guid id,
        UpdateTicketRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ticket = await LoadAsync(id, cancellationToken);

        Guard(() =>
        {
            ticket.Update(request.Title, request.Description, request.Priority, request.DueDateUtc);
            return ticket;
        });

        await db.SaveChangesAsync(cancellationToken);

        return ToResponse(ticket, await ProjectCodeAsync(ticket.ProjectId, cancellationToken));
    }

    public async Task<TicketResponse> ChangeStatusAsync(
        Guid id,
        ChangeTicketStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ticket = await LoadAsync(id, cancellationToken);

        try
        {
            ticket.ChangeStatus(request.Status);
        }
        catch (InvalidOperationException ex)
        {
            // Tranziție nepermisă de mașina de stări din entitate: eroare a clientului.
            throw new BadRequestException(ex.Message);
        }

        await db.SaveChangesAsync(cancellationToken);

        return ToResponse(ticket, await ProjectCodeAsync(ticket.ProjectId, cancellationToken));
    }

    public async Task<TicketResponse> AssignAsync(
        Guid id,
        AssignTicketRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ticket = await LoadAsync(id, cancellationToken);

        if (request.AssignedToUserId is { } assigneeId)
        {
            // Al doilea punct critic: responsabilul trebuie să fie din aceeași organizație.
            // Verificarea rulează filtrat, deci un utilizator străin apare ca inexistent.
            // Este invariantul între agregate pe care entitatea nu-l putea verifica singură.
            var exists = await db.Users.AnyAsync(
                u => u.Id == assigneeId && u.IsActive, cancellationToken);

            if (!exists)
            {
                throw new NotFoundException($"Utilizatorul {assigneeId} nu există în această organizație.");
            }
        }

        Guard(() =>
        {
            ticket.AssignTo(request.AssignedToUserId);
            return ticket;
        });

        await db.SaveChangesAsync(cancellationToken);

        return ToResponse(ticket, await ProjectCodeAsync(ticket.ProjectId, cancellationToken));
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var ticket = await LoadAsync(id, cancellationToken);

        db.Tickets.Remove(ticket);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Ticket> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        await db.Tickets.FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
        ?? throw new NotFoundException($"Tichetul {id} nu există.");

    private async Task<string> ProjectCodeAsync(Guid projectId, CancellationToken cancellationToken) =>
        await db.Projects.AsNoTracking()
            .Where(p => p.Id == projectId)
            .Select(p => p.Code)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

    private Guid RequireUserId() =>
        currentUser.UserId ?? throw new AuthenticationFailedException("Cererea nu este autentificată.");

    private static T Guard<T>(Func<T> action)
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

    // Arbore de expresie, nu metodă: EF Core traduce asta în SELECT cu JOIN spre Projects.
    // Ca apel de metodă, EF n-ar putea să-l traducă și ar evalua pe client, cu Project null.
    private static readonly Expression<Func<Ticket, TicketResponse>> Projection = t =>
        new TicketResponse(t.Id, t.ProjectId, t.Project!.Code, t.Title, t.Description, t.Status,
            t.Priority, t.AssignedToUserId, t.CreatedByUserId, t.DueDateUtc, t.ClosedAtUtc, t.CreatedAtUtc);

    private static TicketResponse ToResponse(Ticket t, string projectCode) =>
        new(t.Id, t.ProjectId, projectCode, t.Title, t.Description, t.Status, t.Priority,
            t.AssignedToUserId, t.CreatedByUserId, t.DueDateUtc, t.ClosedAtUtc, t.CreatedAtUtc);
}
