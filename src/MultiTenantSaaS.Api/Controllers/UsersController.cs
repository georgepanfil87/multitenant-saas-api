using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiTenantSaaS.Application.Common;
using MultiTenantSaaS.Application.Features.Authentication;

namespace MultiTenantSaaS.Api.Controllers;

/// <summary>Administrarea utilizatorilor din organizația curentă.</summary>
[ApiController]
[Route("api/users")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.TenantAdmin)]
public sealed class UsersController(IAuthService authService) : ControllerBase
{
    /// <summary>Creează un utilizator în organizația curentă.</summary>
    /// <remarks>
    /// Organizația nu se trimite în corpul cererii: se ia din tokenul apelantului.
    /// Un administrator nu poate crea utilizatori în altă organizație decât a lui.
    /// </remarks>
    /// <response code="201">Utilizator creat.</response>
    /// <response code="403">Rol insuficient, sau tentativă de a acorda GlobalAdmin.</response>
    /// <response code="409">Emailul există deja în această organizație.</response>
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
