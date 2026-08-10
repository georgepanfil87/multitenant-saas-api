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

                // Each organization gets its own limiter, so an abusive client only burns its
                // own quota. With a single global limiter, one aggressive tenant would throttle
                // everyone else: the noisy-neighbour problem multi-tenancy must prevent.
                var tenant = context.Features.Get<TenantInfo>();

                if (tenant is not null)
                {
                    return CreatePartition($"tenant:{tenant.Id}", options.ResolveLimit(tenant));
                }

                // Tenant-less request (login, signup, probe): partition by IP address.
                var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return CreatePartition($"ip:{ip}", options.AnonymousPerMinute);
            });

            // Separate quota for creating organizations: without it a script could generate
            // thousands of tenants. Applied on top of the global limiter, not instead of it.
            limiter.AddPolicy(RateLimitOptions.RegistrationPolicy, context =>
            {
                var options = context.RequestServices
                    .GetRequiredService<IOptions<RateLimitOptions>>().Value;

                var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

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
                // Retry-After tells the client exactly how long to wait. Without it, clients
                // retry immediately and amplify the very problem we just throttled.
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                }

                await Results.Problem(
                        title: "Too many requests",
                        detail: "The organization quota has been exceeded. Try again later.",
                        statusCode: StatusCodes.Status429TooManyRequests)
                    .ExecuteAsync(context.HttpContext);
            };
        });

        return services;
    }

    private static RateLimitPartition<string> CreatePartition(string key, int requestsPerMinute) =>
        RateLimitPartition.GetTokenBucketLimiter(key, _ => new TokenBucketRateLimiterOptions
        {
            // Bucket capacity is the allowed burst. A page firing 10 calls at once should pass;
            // what must not pass is a sustained rate above the quota.
            TokenLimit = requestsPerMinute,

            // Refilled every second, not once a minute: otherwise this is a fixed window in
            // disguise, with the whole quota available instantly at the top of each minute.
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            TokensPerPeriod = Math.Max(1, requestsPerMinute / 60),

            // No queue: an immediate 429 the client can handle beats a request held in wait,
            // which just looks like a slow application.
            QueueLimit = 0,
            AutoReplenishment = true
        });
}
