using MultiTenantSaaS.Domain.Enums;

namespace MultiTenantSaaS.Application.Common;

/// <summary>
/// Proiecție read-only a unui tenant, folosită de middleware-ul de rezoluție și, la Pasul 8,
/// de rate limiting.
/// </summary>
/// <remarks>
/// Nu returnăm entitatea <c>Tenant</c> pentru că obiectul acesta se cache-uiește: o entitate
/// urmărită de change tracker, ținută în memorie între request-uri, e o sursă sigură de bug-uri.
/// </remarks>
public sealed record TenantInfo(
    Guid Id,
    string Slug,
    string Name,
    SubscriptionPlan Plan,
    bool IsActive,
    int? RequestsPerMinuteOverride);
