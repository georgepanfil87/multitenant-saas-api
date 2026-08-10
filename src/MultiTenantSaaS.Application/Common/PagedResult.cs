using System.ComponentModel.DataAnnotations;

namespace MultiTenantSaaS.Application.Common;

/// <summary>A page of results together with navigation metadata.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasNextPage => Page < TotalPages;
}

/// <summary>Pagination parameters shared by every listing endpoint.</summary>
public sealed record PageRequest
{
    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    /// <summary>Items per page, capped at 100 so a client cannot request the whole table.</summary>
    [Range(1, 100)]
    public int PageSize { get; init; } = 20;

    // A method, not a property: a public property would be listed by ApiExplorer as a
    // query parameter, suggesting a knob the client does not have.
    public int Skip() => (Page - 1) * PageSize;
}
