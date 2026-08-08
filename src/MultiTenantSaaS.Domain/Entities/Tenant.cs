using System.Text.RegularExpressions;
using MultiTenantSaaS.Domain.Common;
using MultiTenantSaaS.Domain.Enums;

namespace MultiTenantSaaS.Domain.Entities;

/// <summary>
/// O organizație client. Rădăcina modelului de izolare: fiecare rând din Users,
/// Projects și Tickets aparține exact unui tenant.
/// </summary>
public sealed partial class Tenant : BaseEntity
{
    /// <summary>
    /// Tenantul rezervat platformei, unde trăiesc conturile de <see cref="UserRole.GlobalAdmin"/>.
    /// Alternativa era <c>Guid? TenantId</c> cu null = platformă; un ID fix ține TenantId
    /// non-nullable peste tot, deci query filter-ul rămâne o singură egalitate.
    /// </summary>
    public static readonly Guid SystemTenantId = new("00000000-0000-0000-0000-000000000001");

    /// <summary>Slug-ul tenantului de sistem.</summary>
    public const string SystemTenantSlug = "system";

    private Tenant()
    {
        Name = string.Empty;
        Slug = string.Empty;
    }

    /// <summary>Numele afișat al organizației.</summary>
    public string Name { get; private set; }

    /// <summary>Identificator URL-safe, unic la nivel de platformă. Cheia de rezoluție a tenantului.</summary>
    public string Slug { get; private set; }

    /// <summary>Planul comercial.</summary>
    public SubscriptionPlan Plan { get; private set; } = SubscriptionPlan.Free;

    /// <summary>Dacă e <c>false</c>, autentificarea e refuzată pentru toți userii organizației.</summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>Limită de request-uri/minut negociată individual. <c>null</c> = limita planului.</summary>
    public int? RequestsPerMinuteOverride { get; private set; }

    /// <summary>Creează o organizație nouă.</summary>
    /// <exception cref="ArgumentException">Dacă numele lipsește sau slug-ul e invalid.</exception>
    public static Tenant Create(string name, string slug, SubscriptionPlan plan = SubscriptionPlan.Free)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        var normalizedSlug = slug.Trim().ToLowerInvariant();

        if (!SlugPattern().IsMatch(normalizedSlug))
        {
            throw new ArgumentException(
                "Slug-ul poate conține doar litere mici, cifre și cratime, trebuie să înceapă " +
                "și să se termine cu caracter alfanumeric, și are între 3 și 63 de caractere.",
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

    /// <summary>Redenumește organizația.</summary>
    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        MarkAsUpdated();
    }

    /// <summary>Schimbă planul comercial.</summary>
    public void ChangePlan(SubscriptionPlan plan)
    {
        Plan = plan;
        MarkAsUpdated();
    }

    /// <summary>Setează o limită de rate limiting individuală, sau o elimină cu <c>null</c>.</summary>
    public void SetRateLimitOverride(int? requestsPerMinute)
    {
        if (requestsPerMinute is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(requestsPerMinute),
                "Limita trebuie să fie pozitivă sau null pentru a folosi limita planului.");
        }

        RequestsPerMinuteOverride = requestsPerMinute;
        MarkAsUpdated();
    }

    /// <summary>Suspendă accesul organizației, păstrând datele.</summary>
    public void Deactivate()
    {
        IsActive = false;
        MarkAsUpdated();
    }

    /// <summary>Reactivează o organizație suspendată.</summary>
    public void Activate()
    {
        IsActive = true;
        MarkAsUpdated();
    }

    // Regulile unei etichete DNS, ca slug-ul să poată fi folosit ca subdomeniu.
    [GeneratedRegex("^[a-z0-9]([a-z0-9-]{1,61}[a-z0-9])$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugPattern();
}
