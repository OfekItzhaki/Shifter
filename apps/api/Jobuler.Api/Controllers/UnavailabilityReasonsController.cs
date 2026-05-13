using Jobuler.Application.Common;
using Jobuler.Application.Spaces.Commands;
using Jobuler.Application.Spaces.Queries;
using Jobuler.Domain.Spaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Jobuler.Api.Controllers;

[ApiController]
[Route("spaces/{spaceId:guid}/unavailability-reasons")]
[Authorize]
public class UnavailabilityReasonsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IPermissionService _permissions;

    public UnavailabilityReasonsController(IMediator mediator, IPermissionService permissions)
    {
        _mediator = mediator;
        _permissions = permissions;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> List(Guid spaceId, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.PeopleManage, ct);
        return Ok(await _mediator.Send(new GetUnavailabilityReasonsQuery(spaceId), ct));
    }

    [HttpPost]
    public async Task<IActionResult> Create(Guid spaceId,
        [FromBody] CreateUnavailabilityReasonRequest req, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.PeopleManage, ct);
        var id = await _mediator.Send(new CreateUnavailabilityReasonCommand(
            spaceId, req.DisplayName, req.SortOrder, CurrentUserId), ct);
        return Created($"/spaces/{spaceId}/unavailability-reasons/{id}", new { id });
    }

    [HttpPut("{reasonId:guid}")]
    public async Task<IActionResult> Update(Guid spaceId, Guid reasonId,
        [FromBody] UpdateUnavailabilityReasonRequest req, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.PeopleManage, ct);
        await _mediator.Send(new UpdateUnavailabilityReasonCommand(
            spaceId, reasonId, req.DisplayName, req.SortOrder, CurrentUserId), ct);
        return NoContent();
    }

    [HttpDelete("{reasonId:guid}")]
    public async Task<IActionResult> Deactivate(Guid spaceId, Guid reasonId, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.PeopleManage, ct);
        await _mediator.Send(new DeactivateUnavailabilityReasonCommand(
            spaceId, reasonId, CurrentUserId), ct);
        return NoContent();
    }

    [HttpPost("seed")]
    public async Task<IActionResult> Seed(Guid spaceId,
        [FromBody] SeedUnavailabilityReasonsRequest req, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.PeopleManage, ct);
        await _mediator.Send(new SeedUnavailabilityReasonsCommand(
            spaceId, req.ReasonDisplayNames, CurrentUserId), ct);
        return NoContent();
    }
}

public record CreateUnavailabilityReasonRequest(string DisplayName, int SortOrder);
public record UpdateUnavailabilityReasonRequest(string DisplayName, int SortOrder);
public record SeedUnavailabilityReasonsRequest(List<string> ReasonDisplayNames);
