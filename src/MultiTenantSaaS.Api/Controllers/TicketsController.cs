using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiTenantSaaS.Application.Common;
using MultiTenantSaaS.Application.Features.Tickets;

namespace MultiTenantSaaS.Api.Controllers;

/// <summary>Tickets of the current organization.</summary>
[ApiController]
[Route("api/tickets")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.Member)]
public sealed class TicketsController(ITicketService tickets) : ControllerBase
{
    /// <summary>Lists tickets, with filters and pagination.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<TicketResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<TicketResponse>>> List(
        [FromQuery] TicketFilter filter,
        [FromQuery] PageRequest pagination,
        CancellationToken cancellationToken)
        => Ok(await tickets.ListAsync(filter, pagination, cancellationToken));

    /// <summary>Returns a ticket by id.</summary>
    /// <response code="404">No such ticket in the current organization.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<TicketResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketResponse>> Get(Guid id, CancellationToken cancellationToken)
        => Ok(await tickets.GetAsync(id, cancellationToken));

    /// <summary>Creates a ticket in a project of the current organization.</summary>
    /// <response code="404">No such project in the current organization.</response>
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

    /// <summary>Updates title, description, priority and due date.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<TicketResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketResponse>> Update(
        Guid id,
        [FromBody] UpdateTicketRequest request,
        CancellationToken cancellationToken)
        => Ok(await tickets.UpdateAsync(id, request, cancellationToken));

    /// <summary>Changes the ticket status.</summary>
    /// <response code="400">The transition is not allowed by the lifecycle.</response>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType<TicketResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TicketResponse>> ChangeStatus(
        Guid id,
        [FromBody] ChangeTicketStatusRequest request,
        CancellationToken cancellationToken)
        => Ok(await tickets.ChangeStatusAsync(id, request, cancellationToken));

    /// <summary>Assigns the ticket to a user of the organization, or unassigns it.</summary>
    /// <response code="404">No such user in the current organization.</response>
    [HttpPatch("{id:guid}/assignee")]
    [ProducesResponseType<TicketResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketResponse>> Assign(
        Guid id,
        [FromBody] AssignTicketRequest request,
        CancellationToken cancellationToken)
        => Ok(await tickets.AssignAsync(id, request, cancellationToken));

    /// <summary>Deletes a ticket.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.TenantAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await tickets.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
