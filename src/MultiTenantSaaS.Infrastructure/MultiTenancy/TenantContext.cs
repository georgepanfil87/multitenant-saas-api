using MultiTenantSaaS.Application.Abstractions;

namespace MultiTenantSaaS.Infrastructure.MultiTenancy;

/// <summary>Default <see cref="ITenantContext"/>. Registered scoped: one instance per request.</summary>
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
            throw new ArgumentException("The current tenant cannot be Guid.Empty.", nameof(tenantId));
        }

        var scope = new TenantScope(this, tenantId, tenantSlug);
        _scopes.Push(scope);
        return scope;
    }

    // A stack, not a single field: onboarding runs in a nested scope, and on exit the previous
    // context must be restored exactly, not reset to "no tenant".
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
                    "Tenant scopes were disposed in a different order than they were created.");
            }

            owner._scopes.Pop();
            _disposed = true;
        }
    }
}
