using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiTenantSaaS.Application.Common;
using MultiTenantSaaS.Application.Features.Authentication;

namespace MultiTenantSaaS.Api.Controllers;

/// <summary>User administration within the caller's organization.</summary>
[ApiController]
[Route("api/users")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.TenantAdmin)]
public sealed class UsersController(IAuthService authService) : ControllerBase
{
    /// <summary>Creates a user in the current organization.</summary>
    /// <remarks>
    /// The organization is taken from the caller's token, so an administrator cannot create
    /// users in an organization other than their own.
    /// </remarks>
    /// <response code="201">User created.</response>
    /// <response code="403">Insufficient role, or an attempt to grant GlobalAdmin.</response>
    /// <response code="409">The email already exists in this organization.</response>
    [HttpPost]
    [ProducesResponseType<UserResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserResponse>> Create(
        [FromBody] CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        var user = await authService.CreateUserAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Create), new { id = user.Id }, user);
    }
}
