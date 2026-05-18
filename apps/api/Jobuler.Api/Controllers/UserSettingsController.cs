using Jobuler.Application.UserSettings.Commands;
using Jobuler.Application.UserSettings.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Jobuler.Api.Controllers;

public record UpdateUserLocationRequest(string CountryCode, string? StateCode);

[ApiController]
[Route("api/user-settings")]
[Authorize]
public class UserSettingsController : ControllerBase
{
    private readonly IMediator _mediator;

    public UserSettingsController(IMediator mediator) => _mediator = mediator;

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPut("location")]
    public async Task<IActionResult> UpdateLocation(
        [FromBody] UpdateUserLocationRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new UpdateUserLocationCommand(CurrentUserId, request.CountryCode, request.StateCode), ct);

        return Ok(new { result.IanaTimezoneId, result.OffsetMinutes });
    }

    [HttpGet]
    public async Task<IActionResult> GetSettings(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetUserSettingsQuery(CurrentUserId), ct);
        return Ok(result);
    }
}
