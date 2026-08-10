using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MultiTenantSaaS.Api.RateLimiting;
using MultiTenantSaaS.Application.Common;
using MultiTenantSaaS.Application.Features.Tenants;

namespace MultiTenantSaaS.Api.Controllers;

/// <summary>Înregistrarea și administrarea organizațiilor.</summary>
[ApiController]
[Route("api/tenants")]
[Produces("application/json")]
public sealed class TenantsController(ITenantOnboardingService onboarding) : ControllerBase
{
    /// <summary>Înregistrează o organizație nouă și primul ei administrator.</summary>
    /// <remarks>
    /// Endpoint public, singurul care creează date fără un tenant deja stabilit.
    /// Creează într-o singură tranzacție: organizația, un utilizator cu rol
    /// <c>TenantAdmin</c>, un proiect implicit și un tichet de bun venit. Returnează
    /// direct un token, deci clientul poate continua fără un login separat.
    /// </remarks>
    /// <response code="201">Organizație creată.</response>
    /// <response code="400">Date invalide (ex: slug cu format greșit).</response>
    /// <response code="409">Slug-ul este deja folosit sau rezervat.</response>
    /// <response code="429">Prea multe înregistrări de la aceeași adresă IP.</response>
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitOptions.RegistrationPolicy)]
    [ProducesResponseType<TenantRegistrationResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TenantRegistrationResponse>> Register(
        [FromBody] RegisterTenantRequest request,
        CancellationToken cancellationToken)
    {
        var result = await onboarding.RegisterAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetAll), new { id = result.Tenant.Id }, result);
    }

    /// <summary>Listează toate organizațiile din platformă.</summary>
    /// <remarks>Rezervat administratorilor de platformă: este singura vedere peste toți tenanții.</remarks>
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.GlobalAdmin)]
    [ProducesResponseType<IReadOnlyList<TenantSummary>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<TenantSummary>>> GetAll(CancellationToken cancellationToken)
        => Ok(await onboarding.ListAllAsync(cancellationToken));
}
