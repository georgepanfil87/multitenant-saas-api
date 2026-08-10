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

        // Fără commit, DisposeAsync face rollback: nu putem uita să anulăm pe calea de eroare.
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
