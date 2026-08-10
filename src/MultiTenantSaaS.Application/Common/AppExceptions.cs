namespace MultiTenantSaaS.Application.Common;

/// <summary>
/// Base for expected application errors, those that map to a specific HTTP status.
/// Anything else is a bug and becomes a 500.
/// </summary>
public abstract class AppException(string message) : Exception(message)
{
    public abstract int StatusCode { get; }

    public abstract string Title { get; }
}

/// <summary>The resource does not exist, or does not exist for the current tenant.</summary>
public sealed class NotFoundException(string message) : AppException(message)
{
    public override int StatusCode => 404;

    public override string Title => "Resource not found";
}

public sealed class BadRequestException(string message) : AppException(message)
{
    public override int StatusCode => 400;

    public override string Title => "Invalid request";
}

public sealed class ConflictException(string message) : AppException(message)
{
    public override int StatusCode => 409;

    public override string Title => "Conflict";
}

/// <summary>Authentication failed. The message is deliberately generic.</summary>
public sealed class AuthenticationFailedException(string message) : AppException(message)
{
    public override int StatusCode => 401;

    public override string Title => "Authentication failed";
}

public sealed class ForbiddenException(string message) : AppException(message)
{
    public override int StatusCode => 403;

    public override string Title => "Access denied";
}
