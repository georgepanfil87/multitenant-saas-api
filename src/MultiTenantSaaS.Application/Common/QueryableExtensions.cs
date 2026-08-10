using Microsoft.EntityFrameworkCore;

namespace MultiTenantSaaS.Application.Common;

public static class QueryableExtensions
{
    /// <summary>
    /// Execută numărarea și pagina într-un query deja filtrat pe tenant.
    /// </summary>
    /// <remarks>
    /// Nu adaugă nicio condiție de tenant: aceasta vine din global query filter, deci
    /// și <c>COUNT</c>-ul e automat per tenant. Dacă filtrarea ar fi fost manuală,
    /// exact aici s-ar fi uitat - iar totalul ar fi scurs numărul de rânduri al tuturor clienților.
    /// </remarks>
    public static async Task<PagedResult<TResult>> ToPagedResultAsync<TResult>(
        this IQueryable<TResult> query,
        PageRequest page,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(page);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.Skip(page.Skip).Take(page.PageSize).ToListAsync(cancellationToken);

        return new PagedResult<TResult>(items, page.Page, page.PageSize, totalCount);
    }
}
