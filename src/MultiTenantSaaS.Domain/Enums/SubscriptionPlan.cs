namespace MultiTenantSaaS.Domain.Enums;

/// <summary>Planul comercial al unui tenant. Determină cotele de rate limiting.</summary>
public enum SubscriptionPlan
{
    Free = 1,
    Pro = 2,
    Enterprise = 3
}
