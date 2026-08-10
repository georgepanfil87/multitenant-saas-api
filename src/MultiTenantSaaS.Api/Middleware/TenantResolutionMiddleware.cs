using Microsoft.Extensions.Options;
using MultiTenantSaaS.Api.MultiTenancy;
using MultiTenantSaaS.Application.Abstractions;
using MultiTenantSaaS.Application.Common;

namespace MultiTenantSaaS.Api.Middleware;

/// <summary>
/// Establishes the current tenant for the lifetime of the request. Runs before any code that
/// touches the database, so the DbContext query filter has something to read.
/// </summary>
public sealed partial class TenantResolutionMiddleware(
    RequestDelegate next,
    IOptions<TenantResolutionOptions> options,
    ILogger<TenantResolutionMiddleware> logger)
{
    // Compile-time generated delegates: no allocations or boxing when the log level is off.
    // This middleware runs on every request, so it matters.
    [LoggerMessage(EventId = 1000, Level = LogLevel.Warning, Message = "Unknown tenant: {Identifier}")]
    private static partial void LogUnknownTenant(ILogger logger, string identifier);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Warning,
        Message = "Tenant from token ({TokenTenant}) differs from the one in the request ({RequestTenant}).")]
    private static partial void LogTenantMismatch(ILogger logger, string tokenTenant, string requestTenant);

    public async Task InvokeAsync(
        HttpContext context,
        IEnumerable<ITenantResolutionStrategy> strategies,
        ITenantStore tenantStore,
        ITenantContext tenantContext)
    {
        var settings = options.Value;

        if (settings.SkipPaths.Any(p => context.Request.Path.StartsWithSegments(p)))
        {
            await next(context);
            return;
        }

        var strategyList = strategies.ToList();
        var fromToken = strategyList
            .FirstOrDefault(s => s.Source == TenantIdentifierSource.Token)?
            .TryResolve(context);

        var fromRequest = strategyList
            .Where(s => s.Source != TenantIdentifierSource.Token)
            .Select(s => s.TryResolve(context))
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

        // The central rule: the token always wins. Without it, a user authenticated in tenant A
        // could send "X-Tenant: b" and get tenant B's context, and every isolation layer below
        // would filter correctly against the wrong tenant.
        var identifier = fromToken ?? fromRequest;

        if (string.IsNullOrWhiteSpace(identifier))
        {
            // Tenant-less request: organization signup, a login without a header, a probe.
            // Not rejected here; endpoints that need data fail closed anyway, because the query
            // filter matches no row.
            await next(context);
            return;
        }

        var tenant = await tenantStore.FindAsync(identifier, context.RequestAborted);

        if (tenant is null)
        {
            LogUnknownTenant(logger, identifier);
            await WriteProblemAsync(context, StatusCodes.Status404NotFound,
                "Unknown tenant", "The requested organization does not exist.");
            return;
        }

        // If the request is authenticated and also carries a header or subdomain, the two must
        // agree. A mismatch is either a misconfigured client or a cross-tenant attempt: refuse
        // rather than guess.
        if (fromToken is not null && fromRequest is not null && !Matches(tenant.Id, tenant.Slug, fromRequest))
        {
            LogTenantMismatch(logger, tenant.Slug, fromRequest);

            await WriteProblemAsync(context, StatusCodes.Status403Forbidden,
                "Inconsistent tenant", "The tenant in the token does not match the one in the request.");
            return;
        }

        if (!tenant.IsActive)
        {
            await WriteProblemAsync(context, StatusCodes.Status403Forbidden,
                "Organization suspended", "Access for this organization is suspended.");
            return;
        }

        // Publish the resolved tenant as a request feature: the rate limiter runs outside the
        // services' DI scope and needs the organization's plan to pick a quota.
        context.Features.Set(tenant);

        using (tenantContext.BeginScope(tenant.Id, tenant.Slug))
        {
            // The scope closes on exit, so the tenant cannot leak into a later request served
            // by the same thread.
            await next(context);
        }
    }

    private static bool Matches(Guid id, string slug, string candidate) =>
        string.Equals(slug, candidate, StringComparison.OrdinalIgnoreCase)
        || (Guid.TryParse(candidate, out var candidateId) && candidateId == id);

    private static Task WriteProblemAsync(HttpContext context, int statusCode, string title, string detail) =>
        Results.Problem(title: title, detail: detail, statusCode: statusCode).ExecuteAsync(context);
}
