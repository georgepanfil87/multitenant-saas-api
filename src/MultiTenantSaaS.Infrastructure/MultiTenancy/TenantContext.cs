using MultiTenantSaaS.Application.Abstractions;

namespace MultiTenantSaaS.Infrastructure.MultiTenancy;

/// <summary>
/// Implementarea implicită a <see cref="ITenantContext"/>. Se înregistrează cu durată de
/// viață <c>Scoped</c>, deci o instanță per request HTTP.
/// </summary>
public sealed class TenantContext : ITenantContext
{
    private readonly Stack<TenantScope> _scopes = new();

    /// <inheritdoc />
    public Guid? TenantId => _scopes.Count > 0 ? _scopes.Peek().TenantId : null;

    /// <inheritdoc />
    public string? TenantSlug => _scopes.Count > 0 ? _scopes.Peek().TenantSlug : null;

    /// <inheritdoc />
    public bool IsResolved => _scopes.Count > 0;

    /// <inheritdoc />
    public IDisposable BeginScope(Guid tenantId, string? tenantSlug = null)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenantul curent nu poate fi Guid.Empty.", nameof(tenantId));
        }

        var scope = new TenantScope(this, tenantId, tenantSlug);
        _scopes.Push(scope);
        return scope;
    }

    // Stivă, nu un simplu câmp: onboarding-ul rulează într-un scope imbricat (creează
    // tenantul, apoi intră în contextul lui ca să scrie userul admin), iar la ieșire
    // trebuie să revină exact la contextul dinainte, nu la "niciun tenant".
    private sealed class TenantScope(TenantContext owner, Guid tenantId, string? tenantSlug) : IDisposable
    {
        private bool _disposed;

        public Guid TenantId { get; } = tenantId;

        public string? TenantSlug { get; } = tenantSlug;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            if (owner._scopes.Count == 0 || !ReferenceEquals(owner._scopes.Peek(), this))
            {
                throw new InvalidOperationException(
                    "Scope-urile de tenant au fost eliberate în altă ordine decât au fost create.");
            }

            owner._scopes.Pop();
            _disposed = true;
        }
    }
}
