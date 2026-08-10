namespace MultiTenantSaaS.Application.Abstractions;

/// <summary>
/// Tranzacții explicite, pentru operațiunile care fac mai multe <c>SaveChanges</c> și trebuie
/// să reușească sau să eșueze în bloc.
/// </summary>
/// <remarks>
/// Abstracția există ca Application să nu depindă de <c>DatabaseFacade</c> din EF Core.
/// Un singur <c>SaveChanges</c> este oricum atomic; asta e pentru cazurile ca onboarding-ul,
/// unde salvăm în doi pași fiindcă al doilea are nevoie de tenantul creat în primul.
/// </remarks>
public interface ITransactionManager
{
    Task<ITransaction> BeginAsync(CancellationToken cancellationToken = default);
}

/// <summary>O tranzacție în curs. Dacă e eliberată fără commit, se face rollback.</summary>
public interface ITransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}
