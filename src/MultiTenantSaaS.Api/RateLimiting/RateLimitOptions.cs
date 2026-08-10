using System.ComponentModel.DataAnnotations;
using MultiTenantSaaS.Application.Common;
using MultiTenantSaaS.Domain.Enums;

namespace MultiTenantSaaS.Api.RateLimiting;

/// <summary>Cotele de request-uri, din secțiunea <c>RateLimiting</c>.</summary>
public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>Numele policy-ului aplicat înregistrării de organizații noi.</summary>
    public const string RegistrationPolicy = "registration";

    [Range(1, 1_000_000)]
    public int FreePerMinute { get; set; } = 60;

    [Range(1, 1_000_000)]
    public int ProPerMinute { get; set; } = 300;

    [Range(1, 1_000_000)]
    public int EnterprisePerMinute { get; set; } = 1000;

    /// <summary>Cotă pentru cererile fără tenant rezolvat, partiționată pe adresă IP.</summary>
    [Range(1, 1_000_000)]
    public int AnonymousPerMinute { get; set; } = 30;

    /// <summary>Cotă separată, strictă, pentru crearea de organizații noi. Per IP, pe oră.</summary>
    [Range(1, 1000)]
    public int RegistrationsPerHour { get; set; } = 5;

    /// <summary>Căi exceptate: sonde de health și documentație.</summary>
    public string[] SkipPaths { get; set; } = ["/health", "/swagger"];

    /// <summary>
    /// Cota efectivă a unei organizații: override-ul negociat individual, altfel cota planului.
    /// </summary>
    /// <remarks>
    /// Limita e o regulă de business, nu o setare de infrastructură: se schimbă la upgrade
    /// de abonament, fără redeploy, pentru că se citește din tenantul rezolvat la fiecare cerere.
    /// </remarks>
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
