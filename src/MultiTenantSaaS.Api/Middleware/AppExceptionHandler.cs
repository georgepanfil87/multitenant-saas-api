using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MultiTenantSaaS.Application.Common;

namespace MultiTenantSaaS.Api.Middleware;

/// <summary>
/// Translates expected application errors into ProblemDetails responses (RFC 7807). Anything
/// not derived from <see cref="AppException"/> is a bug: it is logged in full, but the client
/// gets a generic 500. Stack traces, table names and database messages never leave the process.
/// </summary>
public sealed partial class AppExceptionHandler(ILogger<AppExceptionHandler> logger) : IExceptionHandler
{
    [LoggerMessage(EventId = 2000, Level = LogLevel.Error, Message = "Unhandled exception on {Method} {Path}")]
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
                Title = "Internal error",
                Detail = "An unexpected error occurred."
            };
        }

        httpContext.Response.StatusCode = problem.Status!.Value;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);

        return true;
    }
}
