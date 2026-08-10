using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using MultiTenantSaaS.Api.RateLimiting;
using MultiTenantSaaS.Application.Common;

namespace MultiTenantSaaS.Api.Extensions;

public static class RateLimitingExtensions
{
    public static IServiceCollection AddTenantRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<RateLimitOptions>()
            .Bind(configuration.GetSection(RateLimitOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var options = context.RequestServices
                    .GetRequiredService<IOptions<RateLimitOptions>>().Value;

                if (options.SkipPaths.Any(p => context.Request.Path.StartsWithSegments(p)))
                {
                    return RateLimitPartition.GetNoLimiter("skip");
                }

                // Partiția e cheia întregului pas: fiecare organizație are propriul limitator,
                // deci un client care abuzează își consumă doar propria cotă. Cu un limitator
                // global unic, un singur tenant agresiv ar bloca toți ceilalți clienți -
                // exact „noisy neighbour"-ul pe care multi-tenancy trebuie să-l prevină.
                var tenant = context.Features.Get<TenantInfo>();

                if (tenant is not null)
                {
                    return CreatePartition($"tenant:{tenant.Id}", options.ResolveLimit(tenant));
                }

                // Cerere fără tenant (login, înregistrare, sondă): partiționăm pe IP.
                var ip = context.Connection.RemoteIpAddress?.ToString() ?? "necunoscut";
                return CreatePartition($"ip:{ip}", options.AnonymousPerMinute);
            });

            // Cotă separată pentru crearea de organizații: fără ea, un script ar putea genera
            // mii de tenanți. Se aplică peste limitatorul global, nu în locul lui.
            limiter.AddPolicy(RateLimitOptions.RegistrationPolicy, context =>
            {
                var options = context.RequestServices
                    .GetRequiredService<IOptions<RateLimitOptions>>().Value;

                var ip = context.Connection.RemoteIpAddress?.ToString() ?? "necunoscut";

                return RateLimitPartition.GetFixedWindowLimiter($"register:{ip}", _ =>
                    new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = options.RegistrationsPerHour,
                        Window = TimeSpan.FromHours(1),
                        QueueLimit = 0
                    });
            });

            limiter.OnRejected = async (context, cancellationToken) =>
            {
                // Retry-After îi spune clientului exact cât să aștepte. Fără el, clienții
                // reîncearcă imediat și amplifică problema pe care tocmai am limitat-o.
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                }

                await Results.Problem(
                        title: "Prea multe cereri",
                        detail: "Cota organizației a fost depășită. Reîncearcă mai târziu.",
                        statusCode: StatusCodes.Status429TooManyRequests)
                    .ExecuteAsync(context.HttpContext);
            };
        });

        return services;
    }

    private static RateLimitPartition<string> CreatePartition(string key, int requestsPerMinute) =>
        RateLimitPartition.GetTokenBucketLimiter(key, _ => new TokenBucketRateLimiterOptions
        {
            // Capacitatea găleții = rafala permisă. O pagină care declanșează 10 apeluri
            // deodată trebuie să treacă; ce nu trebuie să treacă e ritmul susținut.
            TokenLimit = requestsPerMinute,

            // Realimentare la fiecare secundă, nu o dată pe minut: altfel ar fi un fixed
            // window deghizat, cu toată cota disponibilă instantaneu la începutul minutului.
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            TokensPerPeriod = Math.Max(1, requestsPerMinute / 60),

            // Fără coadă: preferăm un 429 imediat, pe care clientul îl poate trata,
            // în locul unei cereri ținute în așteptare, care arată ca o aplicație lentă.
            QueueLimit = 0,
            AutoReplenishment = true
        });
}
