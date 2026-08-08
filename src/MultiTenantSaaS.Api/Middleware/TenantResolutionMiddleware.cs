using Microsoft.Extensions.Options;
using MultiTenantSaaS.Api.MultiTenancy;
using MultiTenantSaaS.Application.Abstractions;

namespace MultiTenantSaaS.Api.Middleware;

/// <summary>
/// Stabilește tenantul curent pentru durata cererii. Rulează înaintea oricărui cod care
/// atinge baza de date, ca query filter-ul din <c>ApplicationDbContext</c> să aibă ce citi.
/// </summary>
public sealed partial class TenantResolutionMiddleware(
    RequestDelegate next,
    IOptions<TenantResolutionOptions> options,
    ILogger<TenantResolutionMiddleware> logger)
{
    // Delegate generate la compilare: zero alocări și zero boxing când nivelul de log
    // e dezactivat. Middleware-ul rulează la fiecare cerere, deci contează.
    [LoggerMessage(EventId = 1000, Level = LogLevel.Warning, Message = "Tenant necunoscut: {Identifier}")]
    private static partial void LogUnknownTenant(ILogger logger, string identifier);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Warning,
        Message = "Tenant din token ({TokenTenant}) diferit de cel din cerere ({RequestTenant}).")]
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

        // Regula centrală de securitate a pasului: tokenul câștigă întotdeauna.
        // Fără ea, un utilizator autentificat în tenantul A ar trimite "X-Tenant: b"
        // și ar primi contextul tenantului B - iar toată izolarea de la Pasul 3 ar
        // filtra corect pe tenantul greșit.
        var identifier = fromToken ?? fromRequest;

        if (string.IsNullOrWhiteSpace(identifier))
        {
            // Cerere fără tenant: înregistrare de organizație, login fără header, sondă externă.
            // Nu respingem aici; endpoint-urile care au nevoie de date eșuează închis oricum,
            // pentru că query filter-ul nu se potrivește cu niciun rând.
            await next(context);
            return;
        }

        var tenant = await tenantStore.FindAsync(identifier, context.RequestAborted);

        if (tenant is null)
        {
            LogUnknownTenant(logger, identifier);
            await WriteProblemAsync(context, StatusCodes.Status404NotFound,
                "Tenant necunoscut", "Organizația cerută nu există.");
            return;
        }

        // Dacă cererea e autentificată și aduce și un header/subdomeniu, cele două trebuie
        // să coincidă. Divergența înseamnă fie client prost configurat, fie tentativă de
        // acces încrucișat - în ambele cazuri, refuzăm în loc să ghicim.
        if (fromToken is not null && fromRequest is not null && !Matches(tenant.Id, tenant.Slug, fromRequest))
        {
            LogTenantMismatch(logger, tenant.Slug, fromRequest);

            await WriteProblemAsync(context, StatusCodes.Status403Forbidden,
                "Tenant incoerent", "Tenantul din token nu corespunde celui din cerere.");
            return;
        }

        if (!tenant.IsActive)
        {
            await WriteProblemAsync(context, StatusCodes.Status403Forbidden,
                "Organizație suspendată", "Accesul acestei organizații este suspendat.");
            return;
        }

        using (tenantContext.BeginScope(tenant.Id, tenant.Slug))
        {
            // Scope-ul se închide la ieșirea din using, deci tenantul nu poate „scăpa"
            // într-o cerere ulterioară servită de același thread.
            await next(context);
        }
    }

    private static bool Matches(Guid id, string slug, string candidate) =>
        string.Equals(slug, candidate, StringComparison.OrdinalIgnoreCase)
        || (Guid.TryParse(candidate, out var candidateId) && candidateId == id);

    private static Task WriteProblemAsync(HttpContext context, int statusCode, string title, string detail) =>
        Results.Problem(title: title, detail: detail, statusCode: statusCode).ExecuteAsync(context);
}
