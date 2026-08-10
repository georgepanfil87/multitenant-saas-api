namespace MultiTenantSaaS.Domain.Enums;

/// <summary>Commercial plan of a tenant. Drives the rate limiting quota.</summary>
public enum SubscriptionPlan
{
    Free = 1,
    Pro = 2,
    Enterprise = 3
}
