using System.ComponentModel.DataAnnotations;
using MultiTenantSaaS.Application.Common;
using MultiTenantSaaS.Domain.Enums;

namespace MultiTenantSaaS.Api.RateLimiting;

/// <summary>Request quotas, bound from the "RateLimiting" configuration section.</summary>
public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>Name of the policy applied to new-organization registration.</summary>
    public const string RegistrationPolicy = "registration";

    [Range(1, 1_000_000)]
    public int FreePerMinute { get; set; } = 60;

    [Range(1, 1_000_000)]
    public int ProPerMinute { get; set; } = 300;

    [Range(1, 1_000_000)]
    public int EnterprisePerMinute { get; set; } = 1000;

    /// <summary>Quota for requests without a resolved tenant, partitioned by IP address.</summary>
    [Range(1, 1_000_000)]
    public int AnonymousPerMinute { get; set; } = 30;

    /// <summary>Separate strict quota for creating organizations. Per IP, per hour.</summary>
    [Range(1, 1000)]
    public int RegistrationsPerHour { get; set; } = 5;

    /// <summary>Exempt paths: health probes and documentation.</summary>
    public string[] SkipPaths { get; set; } = ["/health", "/swagger"];

    /// <summary>
    /// Effective quota for an organization: the negotiated override, otherwise the plan's quota.
    /// The limit is a business rule, not an infrastructure setting, so a subscription upgrade
    /// takes effect without a redeploy.
    /// </summary>
    public int ResolveLimit(TenantInfo tenant)
    {
        ArgumentNullException.ThrowIfNull(tenant);

        return tenant.RequestsPerMinuteOverride ?? tenant.Plan switch
        {
            SubscriptionPlan.Free => FreePerMinute,
            SubscriptionPlan.Pro => ProPerMinute,
            SubscriptionPlan.Enterprise => EnterprisePerMinute,
            _ => FreePerMinute
        };
    }
}
