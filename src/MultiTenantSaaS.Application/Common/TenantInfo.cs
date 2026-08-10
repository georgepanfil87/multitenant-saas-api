using MultiTenantSaaS.Domain.Enums;

namespace MultiTenantSaaS.Application.Common;

/// <summary>
/// Read-only projection of a tenant, used by the resolution middleware and by rate limiting.
/// A record rather than the entity, because instances are cached across requests.
/// </summary>
public sealed record TenantInfo(
    Guid Id,
    string Slug,
    string Name,
    SubscriptionPlan Plan,
    bool IsActive,
    int? RequestsPerMinuteOverride);
