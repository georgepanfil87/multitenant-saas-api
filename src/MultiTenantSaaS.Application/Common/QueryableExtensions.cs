using Microsoft.EntityFrameworkCore;

namespace MultiTenantSaaS.Application.Common;

public static class QueryableExtensions
{
    /// <summary>
    /// Runs the count and the page over an already tenant-filtered query. No tenant condition is
    /// added here: the count inherits the global query filter, so the total cannot leak other
    /// tenants' row counts.
    /// </summary>
    public static async Task<PagedResult<TResult>> ToPagedResultAsync<TResult>(
        this IQueryable<TResult> query,
        PageRequest page,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(page);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.Skip(page.Skip()).Take(page.PageSize).ToListAsync(cancellationToken);

        return new PagedResult<TResult>(items, page.Page, page.PageSize, totalCount);
    }
}
