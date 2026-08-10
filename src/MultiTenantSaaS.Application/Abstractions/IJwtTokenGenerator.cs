using MultiTenantSaaS.Domain.Entities;

namespace MultiTenantSaaS.Application.Abstractions;

/// <summary>Emite token-uri de acces pentru utilizatori autentificați.</summary>
public interface IJwtTokenGenerator
{
    /// <summary>
    /// Generează un JWT care poartă identitatea utilizatorului <b>și</b> tenantul lui.
    /// </summary>
    /// <param name="user">Utilizatorul autentificat.</param>
    /// <param name="tenantSlug">Slug-ul organizației, inclus pentru diagnosticare.</param>
    GeneratedToken Generate(User user, string tenantSlug);
}

/// <summary>Tokenul emis, împreună cu momentul expirării.</summary>
public sealed record GeneratedToken(string AccessToken, DateTime ExpiresAtUtc);
