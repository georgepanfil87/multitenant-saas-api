using MultiTenantSaaS.Domain.Common;
using MultiTenantSaaS.Domain.Enums;

namespace MultiTenantSaaS.Domain.Entities;

/// <summary>Entitatea centrală a produsului: o cerere de suport sau sarcină dintr-un proiect.</summary>
public sealed class Ticket : BaseEntity, ITenantEntity
{
    private Ticket()
    {
        Title = string.Empty;
    }

    /// <summary>Organizația proprietară. Ștampilat automat la salvare.</summary>
    public Guid TenantId { get; private set; }

    /// <summary>Proiectul din care face parte tichetul.</summary>
    public Guid ProjectId { get; private set; }

    /// <summary>Navigație către proiect.</summary>
    public Project? Project { get; private set; }

    /// <summary>Titlul.</summary>
    public string Title { get; private set; }

    /// <summary>Descrierea detaliată.</summary>
    public string? Description { get; private set; }

    /// <summary>Starea curentă din ciclul de viață.</summary>
    public TicketStatus Status { get; private set; } = TicketStatus.Open;

    /// <summary>Urgența.</summary>
    public TicketPriority Priority { get; private set; } = TicketPriority.Medium;

    /// <summary>Utilizatorul responsabil. <c>null</c> dacă nu e alocat nimănui.</summary>
    public Guid? AssignedToUserId { get; private set; }

    /// <summary>Utilizatorul care a raportat tichetul.</summary>
    public Guid CreatedByUserId { get; private set; }

    /// <summary>Termen limită opțional, în UTC.</summary>
    public DateTime? DueDateUtc { get; private set; }

    /// <summary>Momentul închiderii, în UTC.</summary>
    public DateTime? ClosedAtUtc { get; private set; }

    /// <summary>Creează un tichet nou, în starea <see cref="TicketStatus.Open"/>.</summary>
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
            throw new ArgumentException("Proiectul este obligatoriu.", nameof(projectId));
        }

        if (createdByUserId == Guid.Empty)
        {
            throw new ArgumentException("Autorul este obligatoriu.", nameof(createdByUserId));
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

    /// <summary>Actualizează câmpurile editabile.</summary>
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
    /// Alocă tichetul unui utilizator, sau îl dezalocă cu <c>null</c>. Verificarea că userul
    /// aparține aceluiași tenant se face în stratul Application, printr-un query filtrat.
    /// </summary>
    public void AssignTo(Guid? userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("Folosește null pentru dezalocare, nu Guid.Empty.", nameof(userId));
        }

        AssignedToUserId = userId;
        MarkAsUpdated();
    }

    /// <summary>Trece tichetul într-o stare nouă, respectând tranzițiile permise.</summary>
    /// <exception cref="InvalidOperationException">Dacă tranziția nu este permisă.</exception>
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
            (TicketStatus.Resolved, TicketStatus.InProgress) => true,  // respins la verificare
            (TicketStatus.Closed, TicketStatus.Open) => true,          // redeschidere
            _ => false
        };

        if (!isAllowed)
        {
            throw new InvalidOperationException($"Tranziția {Status} -> {newStatus} nu este permisă.");
        }

        Status = newStatus;
        ClosedAtUtc = newStatus == TicketStatus.Closed ? DateTime.UtcNow : null;
        MarkAsUpdated();
    }
}
