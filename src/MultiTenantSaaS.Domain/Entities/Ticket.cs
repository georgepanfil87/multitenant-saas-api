using MultiTenantSaaS.Domain.Common;
using MultiTenantSaaS.Domain.Enums;

namespace MultiTenantSaaS.Domain.Entities;

/// <summary>A support request or task inside a project. The product's central entity.</summary>
public sealed class Ticket : BaseEntity, ITenantEntity
{
    private Ticket()
    {
        Title = string.Empty;
    }

    public Guid TenantId { get; private set; }

    public Guid ProjectId { get; private set; }

    public Project? Project { get; private set; }

    public string Title { get; private set; }

    public string? Description { get; private set; }

    public TicketStatus Status { get; private set; } = TicketStatus.Open;

    public TicketPriority Priority { get; private set; } = TicketPriority.Medium;

    public Guid? AssignedToUserId { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public DateTime? DueDateUtc { get; private set; }

    public DateTime? ClosedAtUtc { get; private set; }

    public static Ticket Create(
        Guid projectId,
        string title,
        Guid createdByUserId,
        string? description = null,
        TicketPriority priority = TicketPriority.Medium,
        DateTime? dueDateUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("The project is required.", nameof(projectId));
        }

        if (createdByUserId == Guid.Empty)
        {
            throw new ArgumentException("The author is required.", nameof(createdByUserId));
        }

        return new Ticket
        {
            ProjectId = projectId,
            Title = title.Trim(),
            Description = description?.Trim(),
            CreatedByUserId = createdByUserId,
            Priority = priority,
            DueDateUtc = dueDateUtc,
            Status = TicketStatus.Open
        };
    }

    public void Update(string title, string? description, TicketPriority priority, DateTime? dueDateUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        Title = title.Trim();
        Description = description?.Trim();
        Priority = priority;
        DueDateUtc = dueDateUtc;
        MarkAsUpdated();
    }

    /// <summary>
    /// Assigns the ticket, or clears the assignee with null. Checking that the user belongs to
    /// the same tenant happens in the Application layer, through a filtered query.
    /// </summary>
    public void AssignTo(Guid? userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("Use null to unassign, not Guid.Empty.", nameof(userId));
        }

        AssignedToUserId = userId;
        MarkAsUpdated();
    }

    /// <exception cref="InvalidOperationException">The transition is not allowed.</exception>
    public void ChangeStatus(TicketStatus newStatus)
    {
        if (newStatus == Status)
        {
            return;
        }

        var isAllowed = (Status, newStatus) switch
        {
            (TicketStatus.Open, TicketStatus.InProgress) => true,
            (TicketStatus.Open, TicketStatus.Resolved) => true,
            (TicketStatus.InProgress, TicketStatus.Resolved) => true,
            (TicketStatus.InProgress, TicketStatus.Open) => true,
            (TicketStatus.Resolved, TicketStatus.Closed) => true,
            (TicketStatus.Resolved, TicketStatus.InProgress) => true,  // rejected on verification
            (TicketStatus.Closed, TicketStatus.Open) => true,          // reopened
            _ => false
        };

        if (!isAllowed)
        {
            throw new InvalidOperationException($"The transition {Status} -> {newStatus} is not allowed.");
        }

        Status = newStatus;
        ClosedAtUtc = newStatus == TicketStatus.Closed ? DateTime.UtcNow : null;
        MarkAsUpdated();
    }
}
