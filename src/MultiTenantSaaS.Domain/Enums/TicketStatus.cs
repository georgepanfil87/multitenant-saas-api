namespace MultiTenantSaaS.Domain.Enums;

/// <summary>Stage in a ticket's lifecycle.</summary>
public enum TicketStatus
{
    Open = 1,
    InProgress = 2,
    Resolved = 3,
    Closed = 4
}
