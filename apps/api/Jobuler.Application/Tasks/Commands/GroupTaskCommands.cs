using FluentValidation;
using Jobuler.Application.Common;
using Jobuler.Domain.Spaces;
using Jobuler.Domain.Tasks;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Tasks.Commands;

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record GroupTaskDto(
    Guid Id,
    string Name,
    DateTime StartsAt,
    DateTime EndsAt,
    int ShiftDurationMinutes,
    int RequiredHeadcount,
    string BurdenLevel,
    bool AllowsDoubleShift,
    bool AllowsOverlap,
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
    bool AllowsOverlap) : IRequest<Guid>;

public class CreateGroupTaskCommandValidator : AbstractValidator<CreateGroupTaskCommand>
{
    private static readonly string[] ValidBurdenLevels = ["favorable", "neutral", "disliked", "hated"];

    public CreateGroupTaskCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200).WithMessage("Name must be between 1 and 200 non-blank characters.");
        RuleFor(x => x.EndsAt).GreaterThan(x => x.StartsAt).WithMessage("ends_at must be strictly after starts_at.");
        RuleFor(x => x.ShiftDurationMinutes).GreaterThanOrEqualTo(1).WithMessage("shift_duration_minutes must be at least 1 minute.");
        RuleFor(x => x.RequiredHeadcount).GreaterThanOrEqualTo(1).WithMessage("required_headcount must be at least 1.");
        RuleFor(x => x.BurdenLevel).NotEmpty().Must(b => ValidBurdenLevels.Contains(b.ToLowerInvariant())).WithMessage("burden_level must be one of: favorable, neutral, disliked, hated.");
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
            throw new KeyNotFoundException("Group not found in this space.");

        var task = GroupTask.Create(
            req.SpaceId, req.GroupId, req.Name,
            req.StartsAt, req.EndsAt, req.ShiftDurationMinutes,
            req.RequiredHeadcount,
            Enum.Parse<TaskBurdenLevel>(req.BurdenLevel, true),
            req.AllowsDoubleShift, req.AllowsOverlap,
            req.RequestingUserId);

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
    bool AllowsOverlap) : IRequest;

public class UpdateGroupTaskCommandValidator : AbstractValidator<UpdateGroupTaskCommand>
{
    private static readonly string[] ValidBurdenLevels = ["favorable", "neutral", "disliked", "hated"];

    public UpdateGroupTaskCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200).WithMessage("Name must be between 1 and 200 non-blank characters.");
        RuleFor(x => x.EndsAt).GreaterThan(x => x.StartsAt).WithMessage("ends_at must be strictly after starts_at.");
        RuleFor(x => x.ShiftDurationMinutes).GreaterThanOrEqualTo(1).WithMessage("shift_duration_minutes must be at least 1 minute.");
        RuleFor(x => x.RequiredHeadcount).GreaterThanOrEqualTo(1).WithMessage("required_headcount must be at least 1.");
        RuleFor(x => x.BurdenLevel).NotEmpty().Must(b => ValidBurdenLevels.Contains(b.ToLowerInvariant())).WithMessage("burden_level must be one of: favorable, neutral, disliked, hated.");
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

        task.Update(
            req.Name, req.StartsAt, req.EndsAt,
            req.ShiftDurationMinutes, req.RequiredHeadcount,
            Enum.Parse<TaskBurdenLevel>(req.BurdenLevel, true),
            req.AllowsDoubleShift, req.AllowsOverlap,
            req.RequestingUserId);

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
