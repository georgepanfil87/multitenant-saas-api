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
/// Ticket CRUD. As with projects, no tenant condition appears in this file. The interesting
/// spots are where an id arrives in the request body (project, assignee): those existence
/// checks run filtered, so a cross-tenant reference fails as "not found", not "forbidden".
/// </summary>
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
            // Escape LIKE wildcards, otherwise searching for "100%" would match everything.
            var search = filter.Search.Trim().ToLowerInvariant()
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("%", "\\%", StringComparison.Ordinal)
                .Replace("_", "\\_", StringComparison.Ordinal);

            // Like, not ILike: ILike is Npgsql-specific and would pull the database provider
            // into the Application layer. ToLower gives case insensitivity portably.
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
               ?? throw new NotFoundException($"Ticket {id} does not exist.");
    }

    public async Task<TicketResponse> CreateAsync(
        CreateTicketRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // ProjectId comes from the client. The query is tenant-filtered, so another
        // organization's project is simply not found: 404 with no hand-written check.
        // A 403 would be worse, confirming the id exists somewhere on the platform.
        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == request.ProjectId, cancellationToken)
            ?? throw new NotFoundException($"Project {request.ProjectId} does not exist.");

        if (project.IsArchived)
        {
            throw new BadRequestException("Tickets cannot be added to an archived project.");
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
            // Transition rejected by the entity's state machine: a client error.
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
            // The assignee must belong to the same organization. This filtered check is the
            // cross-aggregate invariant the entity could not enforce on its own.
            var exists = await db.Users.AnyAsync(
                u => u.Id == assigneeId && u.IsActive, cancellationToken);

            if (!exists)
            {
                throw new NotFoundException($"User {assigneeId} does not exist in this organization.");
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
        ?? throw new NotFoundException($"Ticket {id} does not exist.");

    private async Task<string> ProjectCodeAsync(Guid projectId, CancellationToken cancellationToken) =>
        await db.Projects.AsNoTracking()
            .Where(p => p.Id == projectId)
            .Select(p => p.Code)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

    private Guid RequireUserId() =>
        currentUser.UserId ?? throw new AuthenticationFailedException("The request is not authenticated.");

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

    // An expression tree, not a method: EF Core translates this into a SELECT with a JOIN.
    // As a method call it could not be translated and would evaluate client-side, with a
    // null Project navigation.
    private static readonly Expression<Func<Ticket, TicketResponse>> Projection = t =>
        new TicketResponse(t.Id, t.ProjectId, t.Project!.Code, t.Title, t.Description, t.Status,
            t.Priority, t.AssignedToUserId, t.CreatedByUserId, t.DueDateUtc, t.ClosedAtUtc, t.CreatedAtUtc);

    private static TicketResponse ToResponse(Ticket t, string projectCode) =>
        new(t.Id, t.ProjectId, projectCode, t.Title, t.Description, t.Status, t.Priority,
            t.AssignedToUserId, t.CreatedByUserId, t.DueDateUtc, t.ClosedAtUtc, t.CreatedAtUtc);
}
