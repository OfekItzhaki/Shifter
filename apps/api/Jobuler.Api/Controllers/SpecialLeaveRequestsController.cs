using Jobuler.Api.Middleware;
using Jobuler.Application.Common;
using Jobuler.Application.People.SpecialLeave;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Jobuler.Api.Controllers;

[ApiController]
[Route("spaces/{spaceId:guid}/special-leave-requests")]
[Authorize]
public class SpecialLeaveRequestsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IPermissionService _permissions;
    private readonly AppDbContext _db;

    public SpecialLeaveRequestsController(
        IMediator mediator,
        IPermissionService permissions,
        AppDbContext db)
    {
        _mediator = mediator;
        _permissions = permissions;
        _db = db;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("mine")]
    public async Task<IActionResult> Mine(Guid spaceId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.SpaceView, ct);

        var personId = await ResolvePersonIdAsync(spaceId, ct);
        if (personId is null)
            return Forbid();

        return Ok(await _mediator.Send(new GetMySpecialLeaveRequestsQuery(spaceId, personId.Value, from, to), ct));
    }

    [HttpPost]
    public async Task<IActionResult> Submit(Guid spaceId, [FromBody] SubmitSpecialLeaveRequest req, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.SpaceView, ct);

        var personId = await ResolvePersonIdAsync(spaceId, ct);
        if (personId is null)
            return Forbid();

        try
        {
            var id = await _mediator.Send(new SubmitSpecialLeaveRequestCommand(
                spaceId, personId.Value, req.StartsAt, req.EndsAt, req.Reason, CurrentUserId), ct);

            return Created($"/spaces/{spaceId}/special-leave-requests/{id}", new { id });
        }
        catch (InvalidOperationException ex)
        {
            return ProblemDetailsResults.Problem(
                HttpContext,
                statusCode: 422,
                title: "Unprocessable Entity",
                detail: ex.Message,
                typeSlug: "special-leave-request-rejected");
        }
    }

    [HttpPost("{requestId:guid}/cancel")]
    public async Task<IActionResult> CancelMine(Guid spaceId, Guid requestId, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.SpaceView, ct);

        var personId = await ResolvePersonIdAsync(spaceId, ct);
        if (personId is null)
            return Forbid();

        await _mediator.Send(new CancelSpecialLeaveRequestCommand(spaceId, requestId, personId.Value), ct);
        return NoContent();
    }

    [HttpGet("admin")]
    public async Task<IActionResult> ListForAdmin(
        Guid spaceId,
        [FromQuery] string? status,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] Guid? groupId,
        CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.PeopleManage, ct);
        return Ok(await _mediator.Send(new GetSpecialLeaveRequestsForAdminQuery(spaceId, status, from, to, groupId), ct));
    }

    [HttpPost("admin/{requestId:guid}/approve")]
    public async Task<IActionResult> Approve(Guid spaceId, Guid requestId, [FromBody] ReviewSpecialLeaveRequest req, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.PeopleManage, ct);
        var presenceWindowId = await _mediator.Send(new ApproveSpecialLeaveRequestCommand(
            spaceId, requestId, CurrentUserId, req.AdminNote, req.ReasonId), ct);

        return Ok(new { presenceWindowId });
    }

    [HttpPost("admin/{requestId:guid}/reject")]
    public async Task<IActionResult> Reject(Guid spaceId, Guid requestId, [FromBody] ReviewSpecialLeaveRequest req, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.PeopleManage, ct);
        await _mediator.Send(new RejectSpecialLeaveRequestCommand(spaceId, requestId, CurrentUserId, req.AdminNote), ct);
        return NoContent();
    }

    private async Task<Guid?> ResolvePersonIdAsync(Guid spaceId, CancellationToken ct)
    {
        var personId = await _db.People
            .AsNoTracking()
            .Where(p => p.SpaceId == spaceId && p.LinkedUserId == CurrentUserId)
            .Select(p => p.Id)
            .FirstOrDefaultAsync(ct);

        return personId == Guid.Empty ? null : personId;
    }
}

public record SubmitSpecialLeaveRequest(DateTime StartsAt, DateTime EndsAt, string Reason);
public record ReviewSpecialLeaveRequest(string? AdminNote, Guid? ReasonId = null);
