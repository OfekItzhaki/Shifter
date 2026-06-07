using Jobuler.Api.Middleware;
using Jobuler.Application.Common;
using Jobuler.Application.Spaces.SpecialDays;
using Jobuler.Domain.Spaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Jobuler.Api.Controllers;

[ApiController]
[Route("spaces/{spaceId:guid}/special-days")]
[Authorize]
public class SpaceSpecialDaysController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IPermissionService _permissions;

    public SpaceSpecialDaysController(IMediator mediator, IPermissionService permissions)
    {
        _mediator = mediator;
        _permissions = permissions;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> List(
        Guid spaceId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.SpaceView, ct);
        return Ok(await _mediator.Send(new ListSpaceSpecialDaysQuery(spaceId, from, to), ct));
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        Guid spaceId,
        [FromBody] SaveSpaceSpecialDayRequest req,
        CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.ScheduleRecalculate, ct);

        try
        {
            var id = await _mediator.Send(new CreateSpaceSpecialDayCommand(
                spaceId,
                req.Date,
                req.Name,
                req.Kind,
                req.HomeLeaveWeightMultiplier,
                req.RequiresCoverage), ct);

            return Created($"/spaces/{spaceId}/special-days/{id}", new { id });
        }
        catch (ArgumentException ex)
        {
            return InvalidSpecialDay(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return InvalidSpecialDay(ex.Message);
        }
    }

    [HttpPut("{specialDayId:guid}")]
    public async Task<IActionResult> Update(
        Guid spaceId,
        Guid specialDayId,
        [FromBody] SaveSpaceSpecialDayRequest req,
        CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.ScheduleRecalculate, ct);

        try
        {
            await _mediator.Send(new UpdateSpaceSpecialDayCommand(
                spaceId,
                specialDayId,
                req.Date,
                req.Name,
                req.Kind,
                req.HomeLeaveWeightMultiplier,
                req.RequiresCoverage), ct);

            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return InvalidSpecialDay(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return InvalidSpecialDay(ex.Message);
        }
    }

    [HttpDelete("{specialDayId:guid}")]
    public async Task<IActionResult> Delete(Guid spaceId, Guid specialDayId, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.ScheduleRecalculate, ct);

        try
        {
            await _mediator.Send(new DeleteSpaceSpecialDayCommand(spaceId, specialDayId), ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    private IActionResult InvalidSpecialDay(string message) =>
        ProblemDetailsResults.Problem(
            HttpContext,
            statusCode: 422,
            title: "Unprocessable Entity",
            detail: message,
            typeSlug: "invalid-special-day");
}

public record SaveSpaceSpecialDayRequest(
    DateOnly Date,
    string Name,
    SpaceSpecialDayKind Kind,
    decimal HomeLeaveWeightMultiplier,
    bool RequiresCoverage);
