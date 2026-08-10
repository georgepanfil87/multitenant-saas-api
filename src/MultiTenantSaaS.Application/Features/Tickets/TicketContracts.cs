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
    /// <summary>
    /// Proiectul din organizația curentă. Un ID din altă organizație produce 404.
    /// </summary>
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
    /// <summary>Utilizator din organizația curentă, sau <c>null</c> pentru dezalocare.</summary>
    public Guid? AssignedToUserId { get; init; }
}

/// <summary>Filtre de listare. Toate se combină peste query-ul deja izolat pe tenant.</summary>
public sealed record TicketFilter
{
    public Guid? ProjectId { get; init; }

    public TicketStatus? Status { get; init; }

    public TicketPriority? Priority { get; init; }

    public Guid? AssignedToUserId { get; init; }

    /// <summary>Căutare în titlu, case-insensitive.</summary>
    [MaxLength(200)]
    public string? Search { get; init; }
}
