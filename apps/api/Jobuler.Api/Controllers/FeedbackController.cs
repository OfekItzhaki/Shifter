using Jobuler.Application.Feedback.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Jobuler.Api.Controllers;

public record SubmitFeedbackRequest(string Type, string Description);

[ApiController]
[Route("feedback")]
[Authorize]
public class FeedbackController : ControllerBase
{
    private readonly IMediator _mediator;

    public FeedbackController(IMediator mediator) => _mediator = mediator;

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private string CurrentUserEmail =>
        User.FindFirstValue(ClaimTypes.Email)!;

    [HttpPost]
    public async Task<IActionResult> Submit(
        [FromBody] SubmitFeedbackRequest request, CancellationToken ct)
    {
        var normalizedType = request.Type?.Trim().ToLowerInvariant() ?? string.Empty;
        var normalizedDescription = request.Description?.Trim() ?? string.Empty;

        var command = new SubmitFeedbackCommand(
            CurrentUserId,
            CurrentUserEmail,
            normalizedType,
            normalizedDescription);

        await _mediator.Send(command, ct);
        return NoContent();
    }
}
