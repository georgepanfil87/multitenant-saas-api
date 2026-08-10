using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiTenantSaaS.Application.Common;
using MultiTenantSaaS.Application.Features.Projects;

namespace MultiTenantSaaS.Api.Controllers;

/// <summary>
/// Projects of the current organization. The organization never appears as a parameter: it is
/// taken from the caller's token, and another organization's project answers 404, not 403.
/// </summary>
[ApiController]
[Route("api/projects")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.Member)]
public sealed class ProjectsController(IProjectService projects) : ControllerBase
{
    /// <summary>Lists projects, paginated.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<ProjectResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ProjectResponse>>> List(
        [FromQuery] PageRequest pagination,
        [FromQuery] bool includeArchived,
        CancellationToken cancellationToken)
        => Ok(await projects.ListAsync(pagination, includeArchived, cancellationToken));

    /// <summary>Returns a project by id.</summary>
    /// <response code="404">No such project in the current organization.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ProjectResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectResponse>> Get(Guid id, CancellationToken cancellationToken)
        => Ok(await projects.GetAsync(id, cancellationToken));

    /// <summary>Creates a project.</summary>
    /// <response code="409">The code is already used in this organization.</response>
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

    /// <summary>Updates the name and description.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.TenantAdmin)]
    [ProducesResponseType<ProjectResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectResponse>> Update(
        Guid id,
        [FromBody] UpdateProjectRequest request,
        CancellationToken cancellationToken)
        => Ok(await projects.UpdateAsync(id, request, cancellationToken));

    /// <summary>Archives the project: it becomes read-only and drops out of default listings.</summary>
    [HttpPost("{id:guid}/archive")]
    [Authorize(Policy = AuthorizationPolicies.TenantAdmin)]
    [ProducesResponseType<ProjectResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ProjectResponse>> Archive(Guid id, CancellationToken cancellationToken)
        => Ok(await projects.SetArchivedAsync(id, archived: true, cancellationToken));

    /// <summary>Restores an archived project.</summary>
    [HttpPost("{id:guid}/restore")]
    [Authorize(Policy = AuthorizationPolicies.TenantAdmin)]
    [ProducesResponseType<ProjectResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ProjectResponse>> Restore(Guid id, CancellationToken cancellationToken)
        => Ok(await projects.SetArchivedAsync(id, archived: false, cancellationToken));

    /// <summary>Permanently deletes the project and its tickets.</summary>
    /// <remarks>Irreversible. Use archiving for a temporary withdrawal.</remarks>
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
