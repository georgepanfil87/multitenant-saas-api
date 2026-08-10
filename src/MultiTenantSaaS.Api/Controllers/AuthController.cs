using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiTenantSaaS.Application.Common;
using MultiTenantSaaS.Application.Features.Authentication;

namespace MultiTenantSaaS.Api.Controllers;

/// <summary>Authentication and current account details.</summary>
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    /// <summary>Authenticates a user in the organization resolved from the request.</summary>
    /// <remarks>
    /// Send the organization in the X-Tenant header (for example: acme). The same email may
    /// exist in several organizations, so without it we cannot tell which one to sign in to.
    /// </remarks>
    /// <response code="200">Authenticated.</response>
    /// <response code="400">The organization could not be resolved.</response>
    /// <response code="401">Wrong email or password.</response>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
        => Ok(await authService.LoginAsync(request, cancellationToken));

    /// <summary>Returns the currently authenticated account.</summary>
    [HttpGet("me")]
    [Authorize(Policy = AuthorizationPolicies.Member)]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserResponse>> Me(CancellationToken cancellationToken)
        => Ok(await authService.GetCurrentUserAsync(cancellationToken));
}
