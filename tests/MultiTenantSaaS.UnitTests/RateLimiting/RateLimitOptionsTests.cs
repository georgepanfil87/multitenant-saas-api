using MultiTenantSaaS.Api.RateLimiting;
using MultiTenantSaaS.Application.Common;
using MultiTenantSaaS.Domain.Enums;
using Xunit;

namespace MultiTenantSaaS.UnitTests.RateLimiting;

public sealed class RateLimitOptionsTests
{
    private readonly RateLimitOptions _sut = new()
    {
        FreePerMinute = 60,
        ProPerMinute = 300,
        EnterprisePerMinute = 1000
    };

    private static TenantInfo Tenant(SubscriptionPlan plan, int? overrideLimit = null) =>
        new(Guid.NewGuid(), "acme", "Acme", plan, true, overrideLimit);

    [Theory]
    [InlineData(SubscriptionPlan.Free, 60)]
    [InlineData(SubscriptionPlan.Pro, 300)]
    [InlineData(SubscriptionPlan.Enterprise, 1000)]
    public void ResolveLimit_UsesPlanQuota(SubscriptionPlan plan, int expected)
    {
        Assert.Equal(expected, _sut.ResolveLimit(Tenant(plan)));
    }

    [Fact]
    public void ResolveLimit_PrefersPerTenantOverride()
    {
        // A negotiated quota beats the plan: a special contract must not force a new plan
        // into the code.
        Assert.Equal(5000, _sut.ResolveLimit(Tenant(SubscriptionPlan.Pro, 5000)));
    }

    [Fact]
    public void ResolveLimit_OverrideCanAlsoLowerTheQuota()
    {
        // Useful to throttle an abusive client without suspending their access.
        Assert.Equal(5, _sut.ResolveLimit(Tenant(SubscriptionPlan.Enterprise, 5)));
    }
}
