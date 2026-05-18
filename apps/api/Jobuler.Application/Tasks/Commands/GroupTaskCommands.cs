using FluentValidation;
using Jobuler.Application.Common;
using Jobuler.Domain.Scheduling;
using Jobuler.Domain.Spaces;
using Jobuler.Domain.Tasks;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Tasks.Commands;

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record QualificationRequirementDto(string QualificationName, int Count, bool Mandatory);

public record GroupTaskDto(
    Guid Id,
    string Name,
    DateTime StartsAt,
    DateTime EndsAt,
    int ShiftDurationMinutes,
    int RequiredHeadcount,
    string BurdenLevel,
    string EffectiveBurdenLevel,
    int SplitCount,
    bool AllowsDoubleShift,
    bool AllowsOverlap,
    string? DailyStartTime,
    string? DailyEndTime,
    List<QualificationRequirementDto> QualificationRequirements,
    DateTime CreatedAt,
    DateTime UpdatedAt);

// ── Create ────────────────────────────────────────────────────────────────────

public record CreateGroupTaskCommand(
    Guid SpaceId,
    Guid GroupId,
    Guid RequestingUserId,
    string Name,
    DateTime StartsAt,
    DateTime EndsAt,
    int ShiftDurationMinutes,
    int RequiredHeadcount,
    string BurdenLevel,
    bool AllowsDoubleShift,
    bool AllowsOverlap,
    TimeOnly? DailyStartTime = null,
    TimeOnly? DailyEndTime = null,
    List<QualificationRequirementDto>? QualificationRequirements = null,
    int SplitCount = 1) : IRequest<Guid>;

public class CreateGroupTaskCommandValidator : AbstractValidator<CreateGroupTaskCommand>
{
    private static readonly string[] ValidBurdenLevels = ["easy", "normal", "hard"];

    public CreateGroupTaskCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200).WithMessage("Name must be between 1 and 200 non-blank characters.");
        RuleFor(x => x.EndsAt).GreaterThan(x => x.StartsAt).WithMessage("ends_at must be strictly after starts_at.");
        RuleFor(x => x.ShiftDurationMinutes).GreaterThanOrEqualTo(1).WithMessage("shift_duration_minutes must be at least 1 minute.");
        RuleFor(x => x.RequiredHeadcount).GreaterThanOrEqualTo(1).WithMessage("required_headcount must be at least 1.");
        RuleFor(x => x.BurdenLevel).NotEmpty().Must(b => ValidBurdenLevels.Contains(b.ToLowerInvariant())).WithMessage("burden_level must be one of: easy, normal, hard.");
        RuleFor(x => x.SplitCount).GreaterThanOrEqualTo(1).WithMessage("split_count must be at least 1.");

        // Total qualification seats cannot exceed required headcount
        RuleFor(x => x)
            .Must(x => x.QualificationRequirements == null
                || x.QualificationRequirements.Sum(r => r.Count) <= x.RequiredHeadcount)
            .WithMessage("Total qualification seat count across all requirements cannot exceed required_headcount.");
    }
}

public class CreateGroupTaskCommandHandler : IRequestHandler<CreateGroupTaskCommand, Guid>
{
    private readonly AppDbContext _db;
    private readonly IPermissionService _permissions;

    public CreateGroupTaskCommandHandler(AppDbContext db, IPermissionService permissions)
    {
        _db = db;
        _permissions = permissions;
    }

    public async Task<Guid> Handle(CreateGroupTaskCommand req, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(req.RequestingUserId, req.SpaceId, Permissions.TasksManage, ct);

        // Verify group belongs to space
        var groupExists = await _db.Groups
            .AnyAsync(g => g.Id == req.GroupId && g.SpaceId == req.SpaceId && g.DeletedAt == null, ct);
        if (!groupExists)
            throw new KeyNotFoundException("Group not found.");

        var task = GroupTask.Create(
            req.SpaceId, req.GroupId, req.Name,
            TaskTimeHelpers.RoundToHour(req.StartsAt), TaskTimeHelpers.RoundToHour(req.EndsAt),
            req.ShiftDurationMinutes,
            req.RequiredHeadcount,
            Enum.Parse<TaskBurdenLevel>(req.BurdenLevel, true),
            req.AllowsDoubleShift, req.AllowsOverlap,
            req.RequestingUserId,
            req.DailyStartTime, req.DailyEndTime,
            req.QualificationRequirements?
                .Select(r => new QualificationRequirement(r.QualificationName, r.Count, r.Mandatory))
                .ToList(),
            req.SplitCount);

        _db.GroupTasks.Add(task);
        await _db.SaveChangesAsync(ct);
        return task.Id;
    }
}

// ── Update ────────────────────────────────────────────────────────────────────

public record UpdateGroupTaskCommand(
    Guid SpaceId,
    Guid GroupId,
    Guid TaskId,
    Guid RequestingUserId,
    string Name,
    DateTime StartsAt,
    DateTime EndsAt,
    int ShiftDurationMinutes,
    int RequiredHeadcount,
    string BurdenLevel,
    bool AllowsDoubleShift,
    bool AllowsOverlap,
    TimeOnly? DailyStartTime = null,
    TimeOnly? DailyEndTime = null,
    List<QualificationRequirementDto>? QualificationRequirements = null,
    int SplitCount = 1) : IRequest;

public class UpdateGroupTaskCommandValidator : AbstractValidator<UpdateGroupTaskCommand>
{
    private static readonly string[] ValidBurdenLevels = ["easy", "normal", "hard"];

    public UpdateGroupTaskCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200).WithMessage("Name must be between 1 and 200 non-blank characters.");
        RuleFor(x => x.EndsAt).GreaterThan(x => x.StartsAt).WithMessage("ends_at must be strictly after starts_at.");
        RuleFor(x => x.ShiftDurationMinutes).GreaterThanOrEqualTo(1).WithMessage("shift_duration_minutes must be at least 1 minute.");
        RuleFor(x => x.RequiredHeadcount).GreaterThanOrEqualTo(1).WithMessage("required_headcount must be at least 1.");
        RuleFor(x => x.BurdenLevel).NotEmpty().Must(b => ValidBurdenLevels.Contains(b.ToLowerInvariant())).WithMessage("burden_level must be one of: easy, normal, hard.");
        RuleFor(x => x.SplitCount).GreaterThanOrEqualTo(1).WithMessage("split_count must be at least 1.");

        // Total qualification seats cannot exceed required headcount
        RuleFor(x => x)
            .Must(x => x.QualificationRequirements == null
                || x.QualificationRequirements.Sum(r => r.Count) <= x.RequiredHeadcount)
            .WithMessage("Total qualification seat count across all requirements cannot exceed required_headcount.");
    }
}

public class UpdateGroupTaskCommandHandler : IRequestHandler<UpdateGroupTaskCommand>
{
    private readonly AppDbContext _db;
    private readonly IPermissionService _permissions;

    public UpdateGroupTaskCommandHandler(AppDbContext db, IPermissionService permissions)
    {
        _db = db;
        _permissions = permissions;
    }

    public async Task Handle(UpdateGroupTaskCommand req, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(req.RequestingUserId, req.SpaceId, Permissions.TasksManage, ct);

        var task = await _db.GroupTasks
            .FirstOrDefaultAsync(t => t.Id == req.TaskId
                                   && t.GroupId == req.GroupId
                                   && t.SpaceId == req.SpaceId
                                   && t.IsActive, ct)
            ?? throw new KeyNotFoundException("Task not found.");

        var previousAllowsDoubleShift = task.AllowsDoubleShift;

        task.Update(
            req.Name, TaskTimeHelpers.RoundToHour(req.StartsAt), TaskTimeHelpers.RoundToHour(req.EndsAt),
            req.ShiftDurationMinutes, req.RequiredHeadcount,
            Enum.Parse<TaskBurdenLevel>(req.BurdenLevel, true),
            req.AllowsDoubleShift, req.AllowsOverlap,
            req.RequestingUserId,
            req.DailyStartTime, req.DailyEndTime,
            req.QualificationRequirements?
                .Select(r => new QualificationRequirement(r.QualificationName, r.Count, r.Mandatory))
                .ToList(),
            req.SplitCount);

        // Auto-resolve active recommendations when AllowsDoubleShift changes from false to true (Req 5.2)
        if (!previousAllowsDoubleShift && req.AllowsDoubleShift)
        {
            var activeRecommendations = await _db.DoubleShiftRecommendations
                .Where(r => r.GroupTaskId == req.TaskId
                         && r.SpaceId == req.SpaceId
                         && r.Status == RecommendationStatus.Active)
                .ToListAsync(ct);

            foreach (var recommendation in activeRecommendations)
            {
                recommendation.Resolve();
            }
        }

        await _db.SaveChangesAsync(ct);
    }
}

// ── Delete ────────────────────────────────────────────────────────────────────

public record DeleteGroupTaskCommand(
    Guid SpaceId,
    Guid GroupId,
    Guid TaskId,
    Guid RequestingUserId) : IRequest;

public class DeleteGroupTaskCommandHandler : IRequestHandler<DeleteGroupTaskCommand>
{
    private readonly AppDbContext _db;
    private readonly IPermissionService _permissions;

    public DeleteGroupTaskCommandHandler(AppDbContext db, IPermissionService permissions)
    {
        _db = db;
        _permissions = permissions;
    }

    public async Task Handle(DeleteGroupTaskCommand req, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(req.RequestingUserId, req.SpaceId, Permissions.TasksManage, ct);

        var task = await _db.GroupTasks
            .FirstOrDefaultAsync(t => t.Id == req.TaskId
                                   && t.GroupId == req.GroupId
                                   && t.SpaceId == req.SpaceId, ct)
            ?? throw new KeyNotFoundException("Task not found.");

        task.Deactivate(req.RequestingUserId);
        await _db.SaveChangesAsync(ct);
    }
}

// ── Helpers ───────────────────────────────────────────────────────────────────

file static class TaskTimeHelpers
{
    /// <summary>
    /// Rounds a DateTime down to the nearest whole hour.
    /// Ensures task shifts always start/end on clean hour boundaries (e.g. 17:00, not 17:33).
    /// </summary>
    internal static DateTime RoundToHour(DateTime dt) =>
        new(dt.Year, dt.Month, dt.Day, dt.Hour, 0, 0, dt.Kind);
}

