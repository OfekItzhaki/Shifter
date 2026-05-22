using Jobuler.Application.Spaces.Commands;
using Jobuler.Application.Spaces.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Jobuler.Api.Controllers;

[ApiController]
[Route("spaces")]
[Authorize]
public class SpacesController : ControllerBase
{
    private readonly IMediator _mediator;

    public SpacesController(IMediator mediator) => _mediator = mediator;

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>List all spaces the current user belongs to.</summary>
    [HttpGet]
    public async Task<IActionResult> GetMySpaces(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetMySpacesQuery(CurrentUserId), ct);
        return Ok(result);
    }

    /// <summary>Get a single space by ID (extended detail).</summary>
    [HttpGet("{spaceId:guid}")]
    public async Task<IActionResult> GetSpace(Guid spaceId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetSpaceDetailQuery(spaceId, CurrentUserId), ct);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Create a new space. The requesting user becomes the owner.</summary>
    [HttpPost]
    public async Task<IActionResult> CreateSpace([FromBody] CreateSpaceRequest req, CancellationToken ct)
    {
        var spaceId = await _mediator.Send(
            new CreateSpaceCommand(req.Name, req.Description, req.Locale ?? "he", CurrentUserId), ct);
        return CreatedAtAction(nameof(GetSpace), new { spaceId }, new { spaceId });
    }

    /// <summary>Update space settings (owner only).</summary>
    [HttpPut("{spaceId:guid}")]
    public async Task<IActionResult> UpdateSpace(Guid spaceId, [FromBody] UpdateSpaceRequest req, CancellationToken ct)
    {
        await _mediator.Send(
            new UpdateSpaceCommand(spaceId, req.Name, req.Description, req.Locale, CurrentUserId), ct);
        return NoContent();
    }

    /// <summary>Join a space via invite code.</summary>
    [HttpPost("join")]
    public async Task<IActionResult> JoinSpace([FromBody] JoinSpaceRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new JoinSpaceByInviteCodeCommand(req.InviteCode, CurrentUserId), ct);
        return Ok(result);
    }

    /// <summary>Regenerate the space invite code (owner only).</summary>
    [HttpPost("{spaceId:guid}/invite-code/regenerate")]
    public async Task<IActionResult> RegenerateInviteCode(Guid spaceId, CancellationToken ct)
    {
        var newCode = await _mediator.Send(
            new RegenerateSpaceInviteCodeCommand(spaceId, CurrentUserId), ct);
        return Ok(new { inviteCode = newCode });
    }

    /// <summary>Get space members.</summary>
    [HttpGet("{spaceId:guid}/members")]
    public async Task<IActionResult> GetMembers(Guid spaceId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetSpaceMembersQuery(spaceId), ct);
        return Ok(result);
    }

    /// <summary>Trigger migration for existing users without spaces.</summary>
    [HttpPost("migrate")]
    public async Task<IActionResult> MigrateUser(CancellationToken ct)
    {
        var result = await _mediator.Send(new MigrateUserSpaceCommand(CurrentUserId), ct);
        return Ok(result);
    }

    /// <summary>Transfer space ownership to another user.</summary>
    [HttpPost("{spaceId:guid}/transfer-ownership")]
    public async Task<IActionResult> TransferOwnership(
        Guid spaceId, [FromBody] TransferOwnershipRequest req, CancellationToken ct)
    {
        await _mediator.Send(
            new TransferOwnershipCommand(spaceId, req.NewOwnerUserId, CurrentUserId, req.Reason), ct);
        return NoContent();
    }
}

public record CreateSpaceRequest(string Name, string? Description, string? Locale);
public record UpdateSpaceRequest(string Name, string? Description, string Locale);
public record JoinSpaceRequest(string InviteCode);
public record TransferOwnershipRequest(Guid NewOwnerUserId, string? Reason);
