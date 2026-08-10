using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiTenantSaaS.Application.Common;
using MultiTenantSaaS.Application.Features.Authentication;

namespace MultiTenantSaaS.Api.Controllers;

/// <summary>Autentificare și date despre contul curent.</summary>
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    /// <summary>
    /// Autentifică un utilizator în organizația determinată din cerere și returnează un JWT.
    /// </summary>
    /// <remarks>
    /// Organizația se trimite prin headerul <c>X-Tenant</c> (ex: <c>X-Tenant: acme</c>).
    /// Același email poate exista în mai multe organizații, deci fără el nu putem ști
    /// la care dintre ele se autentifică utilizatorul.
    /// </remarks>
    /// <response code="200">Autentificare reușită.</response>
    /// <response code="400">Organizația nu a putut fi determinată.</response>
    /// <response code="401">Email sau parolă incorecte.</response>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
        => Ok(await authService.LoginAsync(request, cancellationToken));

    /// <summary>Returnează contul autentificat curent.</summary>
    [HttpGet("me")]
    [Authorize(Policy = AuthorizationPolicies.Member)]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserResponse>> Me(CancellationToken cancellationToken)
        => Ok(await authService.GetCurrentUserAsync(cancellationToken));
}
