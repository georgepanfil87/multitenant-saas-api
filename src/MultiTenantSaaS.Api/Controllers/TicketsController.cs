using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiTenantSaaS.Application.Common;
using MultiTenantSaaS.Application.Features.Tickets;

namespace MultiTenantSaaS.Api.Controllers;

/// <summary>Tichetele organizației curente.</summary>
[ApiController]
[Route("api/tickets")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.Member)]
public sealed class TicketsController(ITicketService tickets) : ControllerBase
{
    /// <summary>Listează tichetele, cu filtre și paginare.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<TicketResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<TicketResponse>>> List(
        [FromQuery] TicketFilter filter,
        [FromQuery] PageRequest pagination,
        CancellationToken cancellationToken)
        => Ok(await tickets.ListAsync(filter, pagination, cancellationToken));

    /// <summary>Returnează un tichet după ID.</summary>
    /// <response code="404">Tichetul nu există în organizația curentă.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<TicketResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketResponse>> Get(Guid id, CancellationToken cancellationToken)
        => Ok(await tickets.GetAsync(id, cancellationToken));

    /// <summary>Creează un tichet într-un proiect al organizației curente.</summary>
    /// <response code="404">Proiectul nu există în organizația curentă.</response>
    [HttpPost]
    [ProducesResponseType<TicketResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketResponse>> Create(
        [FromBody] CreateTicketRequest request,
        CancellationToken cancellationToken)
    {
        var ticket = await tickets.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = ticket.Id }, ticket);
    }

    /// <summary>Actualizează titlul, descrierea, prioritatea și termenul.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<TicketResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketResponse>> Update(
        Guid id,
        [FromBody] UpdateTicketRequest request,
        CancellationToken cancellationToken)
        => Ok(await tickets.UpdateAsync(id, request, cancellationToken));

    /// <summary>Schimbă starea tichetului.</summary>
    /// <response code="400">Tranziția nu este permisă de ciclul de viață.</response>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType<TicketResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TicketResponse>> ChangeStatus(
        Guid id,
        [FromBody] ChangeTicketStatusRequest request,
        CancellationToken cancellationToken)
        => Ok(await tickets.ChangeStatusAsync(id, request, cancellationToken));

    /// <summary>Alocă tichetul unui utilizator din organizație, sau îl dezalocă.</summary>
    /// <response code="404">Utilizatorul nu există în organizația curentă.</response>
    [HttpPatch("{id:guid}/assignee")]
    [ProducesResponseType<TicketResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketResponse>> Assign(
        Guid id,
        [FromBody] AssignTicketRequest request,
        CancellationToken cancellationToken)
        => Ok(await tickets.AssignAsync(id, request, cancellationToken));

    /// <summary>Șterge un tichet.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.TenantAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await tickets.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
