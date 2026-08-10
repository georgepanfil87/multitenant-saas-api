using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MultiTenantSaaS.Application.Abstractions;

namespace MultiTenantSaaS.Infrastructure.Persistence;

public sealed class EfTransactionManager(ApplicationDbContext db) : ITransactionManager
{
    public async Task<ITransaction> BeginAsync(CancellationToken cancellationToken = default) =>
        new EfTransaction(await db.Database.BeginTransactionAsync(cancellationToken));

    private sealed class EfTransaction(IDbContextTransaction transaction) : ITransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) =>
            transaction.CommitAsync(cancellationToken);

        // Without a commit, DisposeAsync rolls back: the error path cannot forget to undo.
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
