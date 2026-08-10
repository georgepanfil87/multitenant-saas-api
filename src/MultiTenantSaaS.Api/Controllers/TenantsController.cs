using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MultiTenantSaaS.Api.RateLimiting;
using MultiTenantSaaS.Application.Common;
using MultiTenantSaaS.Application.Features.Tenants;

namespace MultiTenantSaaS.Api.Controllers;

/// <summary>Organization registration and administration.</summary>
[ApiController]
[Route("api/tenants")]
[Produces("application/json")]
public sealed class TenantsController(ITenantOnboardingService onboarding) : ControllerBase
{
    /// <summary>Registers a new organization and its first administrator.</summary>
    /// <remarks>
    /// Public endpoint, and the only one that writes data without an existing tenant. Creates
    /// the organization, a TenantAdmin user, a default project and a welcome ticket in a single
    /// transaction, then returns a valid token so no separate login is needed.
    /// </remarks>
    /// <response code="201">Organization created.</response>
    /// <response code="400">Invalid data, for example a malformed slug.</response>
    /// <response code="409">The slug is already taken or reserved.</response>
    /// <response code="429">Too many registrations from the same IP address.</response>
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

    /// <summary>Lists every organization on the platform.</summary>
    /// <remarks>Restricted to platform administrators: it is the only cross-tenant view.</remarks>
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.GlobalAdmin)]
    [ProducesResponseType<IReadOnlyList<TenantSummary>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<TenantSummary>>> GetAll(CancellationToken cancellationToken)
        => Ok(await onboarding.ListAllAsync(cancellationToken));
}
