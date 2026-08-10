namespace MultiTenantSaaS.Application.Abstractions;

/// <summary>
/// Explicit transactions, for operations that call SaveChanges more than once and must
/// succeed or fail as a unit. A single SaveChanges is already atomic.
/// </summary>
public interface ITransactionManager
{
    Task<ITransaction> BeginAsync(CancellationToken cancellationToken = default);
}

/// <summary>An open transaction. Disposing without committing rolls back.</summary>
public interface ITransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}
