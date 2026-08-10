using System.Text.RegularExpressions;
using MultiTenantSaaS.Domain.Common;
using MultiTenantSaaS.Domain.Enums;

namespace MultiTenantSaaS.Domain.Entities;

/// <summary>
/// A client organization. Root of the isolation model: every row in Users, Projects and
/// Tickets belongs to exactly one tenant.
/// </summary>
public sealed partial class Tenant : BaseEntity
{
    // Platform accounts live here. Chosen over a nullable TenantId so the value stays
    // non-nullable everywhere and the query filter remains a single equality check.
    public static readonly Guid SystemTenantId = new("00000000-0000-0000-0000-000000000001");

    public const string SystemTenantSlug = "system";

    private Tenant()
    {
        Name = string.Empty;
        Slug = string.Empty;
    }

    public string Name { get; private set; }

    /// <summary>URL-safe identifier, unique platform-wide. Used to resolve the tenant.</summary>
    public string Slug { get; private set; }

    public SubscriptionPlan Plan { get; private set; } = SubscriptionPlan.Free;

    /// <summary>When false, authentication is refused for every user of the organization.</summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>Individually negotiated quota. Null means the plan's default applies.</summary>
    public int? RequestsPerMinuteOverride { get; private set; }

    /// <exception cref="ArgumentException">Name is missing or slug is malformed.</exception>
    public static Tenant Create(string name, string slug, SubscriptionPlan plan = SubscriptionPlan.Free)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        var normalizedSlug = slug.Trim().ToLowerInvariant();

        if (!SlugPattern().IsMatch(normalizedSlug))
        {
            throw new ArgumentException(
                "The slug may contain only lowercase letters, digits and hyphens, must start " +
                "and end with an alphanumeric character, and be 3 to 63 characters long.",
                nameof(slug));
        }

        return new Tenant
        {
            Name = name.Trim(),
            Slug = normalizedSlug,
            Plan = plan,
            IsActive = true
        };
    }

    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        MarkAsUpdated();
    }

    public void ChangePlan(SubscriptionPlan plan)
    {
        Plan = plan;
        MarkAsUpdated();
    }

    public void SetRateLimitOverride(int? requestsPerMinute)
    {
        if (requestsPerMinute is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(requestsPerMinute),
                "The limit must be positive, or null to fall back to the plan quota.");
        }

        RequestsPerMinuteOverride = requestsPerMinute;
        MarkAsUpdated();
    }

    /// <summary>Suspends access while keeping the organization's data.</summary>
    public void Deactivate()
    {
        IsActive = false;
        MarkAsUpdated();
    }

    public void Activate()
    {
        IsActive = true;
        MarkAsUpdated();
    }

    // DNS label rules, so the slug can also be used as a subdomain.
    [GeneratedRegex("^[a-z0-9]([a-z0-9-]{1,61}[a-z0-9])$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugPattern();
}
