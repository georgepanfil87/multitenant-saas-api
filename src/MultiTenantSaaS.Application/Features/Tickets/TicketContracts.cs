using System.ComponentModel.DataAnnotations;
using MultiTenantSaaS.Domain.Enums;

namespace MultiTenantSaaS.Application.Features.Tickets;

public sealed record TicketResponse(
    Guid Id,
    Guid ProjectId,
    string ProjectCode,
    string Title,
    string? Description,
    TicketStatus Status,
    TicketPriority Priority,
    Guid? AssignedToUserId,
    Guid CreatedByUserId,
    DateTime? DueDateUtc,
    DateTime? ClosedAtUtc,
    DateTime CreatedAtUtc);

public sealed record CreateTicketRequest
{
    /// <summary>A project in the current organization. An id from another one returns 404.</summary>
    [Required]
    public Guid ProjectId { get; init; }

    [Required]
    [MaxLength(300)]
    public string Title { get; init; } = string.Empty;

    [MaxLength(5000)]
    public string? Description { get; init; }

    public TicketPriority Priority { get; init; } = TicketPriority.Medium;

    public DateTime? DueDateUtc { get; init; }
}

public sealed record UpdateTicketRequest
{
    [Required]
    [MaxLength(300)]
    public string Title { get; init; } = string.Empty;

    [MaxLength(5000)]
    public string? Description { get; init; }

    public TicketPriority Priority { get; init; } = TicketPriority.Medium;

    public DateTime? DueDateUtc { get; init; }
}

public sealed record ChangeTicketStatusRequest
{
    [Required]
    public TicketStatus Status { get; init; }
}

public sealed record AssignTicketRequest
{
    /// <summary>A user in the current organization, or null to unassign.</summary>
    public Guid? AssignedToUserId { get; init; }
}

/// <summary>Listing filters. All are applied on top of the tenant-filtered query.</summary>
public sealed record TicketFilter
{
    public Guid? ProjectId { get; init; }

    public TicketStatus? Status { get; init; }

    public TicketPriority? Priority { get; init; }

    public Guid? AssignedToUserId { get; init; }

    /// <summary>Case-insensitive search in the title.</summary>
    [MaxLength(200)]
    public string? Search { get; init; }
}
