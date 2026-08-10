namespace MultiTenantSaaS.Application.Common;

/// <summary>
/// Bază pentru erorile așteptate ale aplicației, cele care se traduc într-un cod HTTP
/// anume. Orice altă excepție este un bug și devine 500.
/// </summary>
public abstract class AppException(string message) : Exception(message)
{
    public abstract int StatusCode { get; }

    public abstract string Title { get; }
}

/// <summary>Resursa nu există - sau nu există <b>pentru tenantul curent</b>.</summary>
public sealed class NotFoundException(string message) : AppException(message)
{
    public override int StatusCode => 404;

    public override string Title => "Resursă inexistentă";
}

/// <summary>Cererea e validă ca formă, dar încalcă o regulă de business.</summary>
public sealed class BadRequestException(string message) : AppException(message)
{
    public override int StatusCode => 400;

    public override string Title => "Cerere invalidă";
}

/// <summary>Conflict cu starea curentă: email duplicat, cod de proiect deja folosit.</summary>
public sealed class ConflictException(string message) : AppException(message)
{
    public override int StatusCode => 409;

    public override string Title => "Conflict";
}

/// <summary>Autentificare eșuată. Mesajul este intenționat generic.</summary>
public sealed class AuthenticationFailedException(string message) : AppException(message)
{
    public override int StatusCode => 401;

    public override string Title => "Autentificare eșuată";
}

/// <summary>Utilizator autentificat, dar fără dreptul de a face operațiunea.</summary>
public sealed class ForbiddenException(string message) : AppException(message)
{
    public override int StatusCode => 403;

    public override string Title => "Acces interzis";
}
