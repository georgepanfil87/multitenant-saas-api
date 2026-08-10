using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiTenantSaaS.Application.Common;
using MultiTenantSaaS.Application.Features.Projects;

namespace MultiTenantSaaS.Api.Controllers;

/// <summary>Proiectele organizației curente.</summary>
/// <remarks>
/// Organizația nu apare în niciun parametru: se ia din tokenul apelantului.
/// Un proiect al altei organizații răspunde cu 404, nu cu 403.
/// </remarks>
[ApiController]
[Route("api/projects")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.Member)]
public sealed class ProjectsController(IProjectService projects) : ControllerBase
{
    /// <summary>Listează proiectele, paginat.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<ProjectResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ProjectResponse>>> List(
        [FromQuery] PageRequest pagination,
        [FromQuery] bool includeArchived,
        CancellationToken cancellationToken)
        => Ok(await projects.ListAsync(pagination, includeArchived, cancellationToken));

    /// <summary>Returnează un proiect după ID.</summary>
    /// <response code="404">Proiectul nu există în organizația curentă.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ProjectResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectResponse>> Get(Guid id, CancellationToken cancellationToken)
        => Ok(await projects.GetAsync(id, cancellationToken));

    /// <summary>Creează un proiect.</summary>
    /// <response code="409">Codul este deja folosit în această organizație.</response>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.TenantAdmin)]
    [ProducesResponseType<ProjectResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProjectResponse>> Create(
        [FromBody] CreateProjectRequest request,
        CancellationToken cancellationToken)
    {
        var project = await projects.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = project.Id }, project);
    }

    /// <summary>Actualizează numele și descrierea.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.TenantAdmin)]
    [ProducesResponseType<ProjectResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectResponse>> Update(
        Guid id,
        [FromBody] UpdateProjectRequest request,
        CancellationToken cancellationToken)
        => Ok(await projects.UpdateAsync(id, request, cancellationToken));

    /// <summary>Arhivează proiectul: devine read-only și dispare din listările implicite.</summary>
    [HttpPost("{id:guid}/archive")]
    [Authorize(Policy = AuthorizationPolicies.TenantAdmin)]
    [ProducesResponseType<ProjectResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ProjectResponse>> Archive(Guid id, CancellationToken cancellationToken)
        => Ok(await projects.SetArchivedAsync(id, archived: true, cancellationToken));

    /// <summary>Scoate proiectul din arhivă.</summary>
    [HttpPost("{id:guid}/restore")]
    [Authorize(Policy = AuthorizationPolicies.TenantAdmin)]
    [ProducesResponseType<ProjectResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ProjectResponse>> Restore(Guid id, CancellationToken cancellationToken)
        => Ok(await projects.SetArchivedAsync(id, archived: false, cancellationToken));

    /// <summary>Șterge definitiv proiectul și tichetele lui.</summary>
    /// <remarks>Ștergerea e ireversibilă. Pentru retragere temporară, folosește arhivarea.</remarks>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.TenantAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await projects.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
