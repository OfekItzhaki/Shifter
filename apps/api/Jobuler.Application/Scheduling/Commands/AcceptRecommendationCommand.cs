using FluentValidation;
using Jobuler.Application.Common;
using Jobuler.Domain.Scheduling;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Scheduling.Commands;

public record AcceptRecommendationCommand(
    Guid SpaceId,
    Guid RecommendationId,
    Guid UserId,
    bool TriggerNewRun) : IRequest<AcceptRecommendationResult>;

public enum AcceptRecommendationOutcome
{
    Accepted,
    AlreadyEnabled,
    TaskNotFound
}

public record AcceptRecommendationResult(
    AcceptRecommendationOutcome Outcome,
    string Message,
    Guid? EnqueuedRunId = null);

public class AcceptRecommendationCommandValidator : AbstractValidator<AcceptRecommendationCommand>
{
    public AcceptRecommendationCommandValidator()
    {
        RuleFor(x => x.SpaceId).NotEmpty();
        RuleFor(x => x.RecommendationId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
    }
}

public class AcceptRecommendationCommandHandler : IRequestHandler<AcceptRecommendationCommand, AcceptRecommendationResult>
{
    private readonly AppDbContext _db;
    private readonly IPermissionService _permissions;
    private readonly IMediator _mediator;

    public AcceptRecommendationCommandHandler(
        AppDbContext db,
        IPermissionService permissions,
        IMediator mediator)
    {
        _db = db;
        _permissions = permissions;
        _mediator = mediator;
    }

    public async Task<AcceptRecommendationResult> Handle(AcceptRecommendationCommand req, CancellationToken ct)
    {
        // Permission check — require TasksManage (ViewAndEdit or Owner level)
        await _permissions.RequirePermissionAsync(req.UserId, req.SpaceId, Permissions.TasksManage, ct);

        // Load recommendation and verify it belongs to the space
        var recommendation = await _db.DoubleShiftRecommendations
            .FirstOrDefaultAsync(r => r.Id == req.RecommendationId && r.SpaceId == req.SpaceId, ct)
            ?? throw new KeyNotFoundException("Recommendation not found.");

        // Load the referenced GroupTask
        var task = await _db.GroupTasks
            .FirstOrDefaultAsync(t => t.Id == recommendation.GroupTaskId && t.SpaceId == req.SpaceId, ct);

        // If task not found, mark recommendation as Cleared and return 404
        if (task == null)
        {
            recommendation.Clear();
            await _db.SaveChangesAsync(ct);
            throw new KeyNotFoundException(
                "The referenced task no longer exists. Recommendation has been cleared.");
        }

        // If AllowsDoubleShift is already true, return informational message and mark as Resolved
        if (task.AllowsDoubleShift)
        {
            recommendation.Resolve();
            await _db.SaveChangesAsync(ct);
            return new AcceptRecommendationResult(
                AcceptRecommendationOutcome.AlreadyEnabled,
                $"Task '{task.Name}' already has double shifts enabled. Recommendation resolved.");
        }

        // Enable double shift on the task
        task.EnableDoubleShift(req.UserId);
        recommendation.Resolve();
        await _db.SaveChangesAsync(ct);

        // If TriggerNewRun is true, enqueue a new solver run for the group
        Guid? runId = null;
        if (req.TriggerNewRun)
        {
            runId = await _mediator.Send(new TriggerSolverCommand(
                SpaceId: req.SpaceId,
                TriggerMode: "standard",
                RequestedByUserId: req.UserId,
                GroupId: task.GroupId), ct);
        }

        return new AcceptRecommendationResult(
            AcceptRecommendationOutcome.Accepted,
            $"Double shift enabled on task '{task.Name}'.",
            runId);
    }
}
