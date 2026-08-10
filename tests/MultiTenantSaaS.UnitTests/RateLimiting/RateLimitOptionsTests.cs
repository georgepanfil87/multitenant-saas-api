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
        // Cota negociată individual bate planul: un client Enterprise cu contract special
        // nu trebuie să ne oblige să inventăm un plan nou în cod.
        Assert.Equal(5000, _sut.ResolveLimit(Tenant(SubscriptionPlan.Pro, 5000)));
    }

    [Fact]
    public void ResolveLimit_OverrideCanAlsoLowerTheQuota()
    {
        // Util pentru a tempera un client care abuzează, fără să-i suspendăm accesul.
        Assert.Equal(5, _sut.ResolveLimit(Tenant(SubscriptionPlan.Enterprise, 5)));
    }
}
