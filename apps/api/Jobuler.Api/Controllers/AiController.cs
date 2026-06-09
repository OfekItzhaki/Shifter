using Jobuler.Application.AI;
using Jobuler.Application.AI.Commands;
using Jobuler.Application.Common;
using Jobuler.Domain.Spaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Jobuler.Api.Controllers;

/// <summary>
/// AI assistant endpoints — optional helper layer.
/// All responses are candidate data for admin review, never auto-applied.
/// Requires admin_mode permission.
/// </summary>
[ApiController]
[Route("spaces/{spaceId:guid}/ai")]
[Authorize]
public class AiController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IPermissionService _permissions;

    public AiController(IMediator mediator, IPermissionService permissions)
    {
        _mediator = mediator;
        _permissions = permissions;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// User-facing native Shifter assistant.
    /// The assistant can explain workflows and suggest safe next actions, but never mutates data.
    /// </summary>
    [HttpPost("chat")]
    public async Task<IActionResult> Chat(
        Guid spaceId, [FromBody] AiChatRequest req, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(
            CurrentUserId, spaceId, Permissions.SpaceView, ct);

        var locale = User.FindFirstValue("locale") ?? req.Locale ?? "he";
        var userDisplayName = User.FindFirstValue("name") ?? User.Identity?.Name;

        var result = await _mediator.Send(new ChatCommand(
            req.Message,
            locale,
            userDisplayName,
            req.CurrentPath,
            IsAuthenticated: true,
            req.IsAdminMode,
            req.RecentMessages ?? []), ct);

        return Ok(result);
    }

    /// <summary>
    /// Parse a natural language instruction into a candidate constraint.
    /// The admin must review and confirm before saving.
    /// </summary>
    [HttpPost("parse-constraint")]
    public async Task<IActionResult> ParseConstraint(
        Guid spaceId, [FromBody] ParseConstraintRequest req, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(
            CurrentUserId, spaceId, Permissions.ConstraintsManage, ct);

        var locale = User.FindFirstValue("locale") ?? "he";
        var result = await _mediator.Send(
            new ParseConstraintCommand(spaceId, req.Input, locale, CurrentUserId), ct);

        return Ok(result);
    }

    /// <summary>
    /// Summarize a schedule diff in plain language.
    /// </summary>
    [HttpPost("summarize-diff")]
    public async Task<IActionResult> SummarizeDiff(
        Guid spaceId, [FromBody] DiffContextDto diff, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(
            CurrentUserId, spaceId, Permissions.SpaceAdminMode, ct);

        var locale = User.FindFirstValue("locale") ?? "he";
        var summary = await _mediator.Send(new SummarizeDiffCommand(diff, locale), ct);
        return Ok(new { summary });
    }

    /// <summary>
    /// Explain why a schedule was infeasible.
    /// </summary>
    [HttpPost("explain-infeasibility")]
    public async Task<IActionResult> ExplainInfeasibility(
        Guid spaceId, [FromBody] InfeasibilityContextDto context, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(
            CurrentUserId, spaceId, Permissions.SpaceAdminMode, ct);

        var locale = User.FindFirstValue("locale") ?? "he";
        var explanation = await _mediator.Send(
            new ExplainInfeasibilityCommand(context, locale), ct);
        return Ok(new { explanation });
    }

    // Smart import endpoints moved to ImportController
}

public record ParseConstraintRequest(string Input);

public record AiChatRequest(
    string Message,
    string? Locale,
    string? CurrentPath,
    bool IsAdminMode,
    IReadOnlyList<AiChatMessageDto>? RecentMessages);
