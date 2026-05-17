using FluentValidation;
using Jobuler.Application.Common;
using Jobuler.Application.Scheduling;
using Jobuler.Application.Scheduling.Commands;
using Jobuler.Application.Scheduling.Models;
using Jobuler.Domain.Spaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Jobuler.Api.Controllers;

[ApiController]
[Route("spaces/{spaceId:guid}/groups/{groupId:guid}")]
[Authorize]
public class SimulationController : ControllerBase
{
    private readonly IPermissionService _permissions;
    private readonly ISolverClient _solverClient;
    private readonly IValidator<SimulateRequest> _simulateValidator;
    private readonly IValidator<PublishSandboxRequest> _publishValidator;
    private readonly IMediator _mediator;

    public SimulationController(
        IPermissionService permissions,
        ISolverClient solverClient,
        IValidator<SimulateRequest> simulateValidator,
        IValidator<PublishSandboxRequest> publishValidator,
        IMediator mediator)
    {
        _permissions = permissions;
        _solverClient = solverClient;
        _simulateValidator = simulateValidator;
        _publishValidator = publishValidator;
        _mediator = mediator;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Run a simulation with the provided solver payload.
    /// Calls the solver synchronously and returns the result without creating any database records.
    /// Admin-only (group owner / space owner).
    /// </summary>
    [HttpPost("simulate")]
    public async Task<IActionResult> Simulate(
        Guid spaceId, Guid groupId,
        [FromBody] SimulateRequest request, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(
            CurrentUserId, spaceId, Permissions.ScheduleRecalculate, ct);

        var validationResult = await _simulateValidator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            throw new ValidationException(validationResult.Errors);

        var result = await _solverClient.SolveAsync(request.Payload, ct);

        return Ok(result);
    }

    /// <summary>
    /// Publish a sandbox: persist all overrides and publish the draft version in a single transaction.
    /// Returns 409 Conflict if the version is already published or discarded.
    /// </summary>
    [HttpPost("publish-sandbox")]
    public async Task<IActionResult> PublishSandbox(
        Guid spaceId, Guid groupId,
        [FromBody] PublishSandboxRequest request, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(
            CurrentUserId, spaceId, Permissions.SchedulePublish, ct);

        var validationResult = await _publishValidator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            throw new ValidationException(validationResult.Errors);

        try
        {
            await _mediator.Send(new PublishSandboxCommand(
                spaceId, groupId, CurrentUserId, request), ct);
        }
        catch (InvalidOperationException ex)
        {
            throw new ConflictException(ex.Message);
        }

        return NoContent();
    }
}
