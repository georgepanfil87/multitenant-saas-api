using System.ComponentModel.DataAnnotations;

namespace MultiTenantSaaS.Application.Common;

/// <summary>O pagină de rezultate, împreună cu informațiile de navigare.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasNextPage => Page < TotalPages;
}

/// <summary>Parametrii de paginare, comuni tuturor listărilor.</summary>
public sealed record PageRequest
{
    /// <summary>Numărul paginii, începând de la 1.</summary>
    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    /// <summary>
    /// Câte elemente pe pagină. Plafonat la 100, ca un client să nu poată cere
    /// întreaga tabelă printr-un singur request.
    /// </summary>
    [Range(1, 100)]
    public int PageSize { get; init; } = 20;

    public int Skip => (Page - 1) * PageSize;
}
