using System.ComponentModel.DataAnnotations;

namespace MultiTenantSaaS.Application.Features.Projects;

public sealed record ProjectResponse(
    Guid Id,
    string Name,
    string Code,
    string? Description,
    bool IsArchived,
    Guid CreatedByUserId,
    int TicketCount,
    DateTime CreatedAtUtc);

public sealed record CreateProjectRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; init; } = string.Empty;

    /// <summary>Cod scurt, unic în cadrul organizației. Ex: <c>SUP</c>.</summary>
    [Required]
    [MinLength(2)]
    [MaxLength(10)]
    public string Code { get; init; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; init; }
}

public sealed record UpdateProjectRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; init; }
}
