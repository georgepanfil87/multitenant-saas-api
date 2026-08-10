using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MultiTenantSaaS.Application.Common;

namespace MultiTenantSaaS.Api.Middleware;

/// <summary>
/// Traduce excepțiile așteptate ale aplicației în răspunsuri ProblemDetails (RFC 7807).
/// </summary>
/// <remarks>
/// Orice excepție care nu derivă din <see cref="AppException"/> este un bug: se loghează
/// integral, dar clientul primește un 500 generic. Detaliile interne - stack trace, nume de
/// tabele, mesaje de la PostgreSQL - nu ies niciodată din proces.
/// </remarks>
public sealed partial class AppExceptionHandler(ILogger<AppExceptionHandler> logger) : IExceptionHandler
{
    [LoggerMessage(EventId = 2000, Level = LogLevel.Error, Message = "Eroare netratată la {Method} {Path}")]
    private static partial void LogUnhandled(ILogger logger, Exception exception, string method, string path);

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var problem = exception switch
        {
            AppException app => new ProblemDetails
            {
                Status = app.StatusCode,
                Title = app.Title,
                Detail = app.Message
            },
            _ => null
        };

        if (problem is null)
        {
            LogUnhandled(logger, exception, httpContext.Request.Method, httpContext.Request.Path);

            problem = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Eroare internă",
                Detail = "A apărut o eroare neașteptată."
            };
        }

        httpContext.Response.StatusCode = problem.Status!.Value;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);

        return true;
    }
}
