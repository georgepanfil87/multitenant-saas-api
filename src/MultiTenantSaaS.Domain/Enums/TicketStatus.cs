namespace MultiTenantSaaS.Domain.Enums;

/// <summary>Starea din ciclul de viață al unui tichet.</summary>
public enum TicketStatus
{
    Open = 1,
    InProgress = 2,
    Resolved = 3,
    Closed = 4
}
