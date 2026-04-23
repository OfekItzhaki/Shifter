using Jobuler.Application.Groups.Commands;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Jobuler.Api.Controllers;

/// <summary>
/// Public endpoint — no auth required. Uses opt-out token from notification.
/// </summary>
[ApiController]
[Route("group-opt-out")]
public class GroupOptOutController : ControllerBase
{
    private readonly IMediator _mediator;
    public GroupOptOutController(IMediator mediator) => _mediator = mediator;

    [HttpPost("{token}")]
    public async Task<IActionResult> OptOut(string token, CancellationToken ct)
    {
        var result = await _mediator.Send(new LeaveGroupByTokenCommand(token), ct);
        if (!result.Success) return NotFound(new { error = "טוקן לא תקין או כבר בוצעה יציאה." });
        return Ok(new { groupName = result.GroupName });
    }
}
