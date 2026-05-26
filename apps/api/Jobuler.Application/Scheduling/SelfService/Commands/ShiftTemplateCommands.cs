using FluentValidation;
using Jobuler.Application.Common;
using Jobuler.Domain.Scheduling;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Scheduling.SelfService.Commands;

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record ShiftTemplateDto(
    Guid Id,
    Guid GroupId,
    Guid GroupTaskId,
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int RequiredHeadcount,
    bool IsDeleted,
    DateTime CreatedAt,
    DateTime UpdatedAt);

// ── Create ────────────────────────────────────────────────────────────────────

public record CreateShiftTemplateCommand(
    Guid SpaceId,
    Guid GroupId,
    Guid GroupTaskId,
    Guid RequestingUserId,
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int RequiredHeadcount) : IRequest<ShiftTemplateDto>;

public class CreateShiftTemplateCommandValidator : AbstractValidator<CreateShiftTemplateCommand>
{
    public CreateShiftTemplateCommandValidator()
    {
        RuleFor(x => x.SpaceId).NotEmpty();
        RuleFor(x => x.GroupId).NotEmpty();
        RuleFor(x => x.GroupTaskId).NotEmpty();
        RuleFor(x => x.RequestingUserId).NotEmpty();

        RuleFor(x => x.DayOfWeek).IsInEnum()
            .WithMessage("Day of week must be a valid value (Sunday through Saturday).");

        RuleFor(x => x.StartTime)
            .LessThan(x => x.EndTime)
            .WithMessage("Start time must be before end time.");

        RuleFor(x => x.RequiredHeadcount)
            .InclusiveBetween(1, 999)
            .WithMessage("Required headcount must be between 1 and 999.");
    }
}

public class CreateShiftTemplateCommandHandler : IRequestHandler<CreateShiftTemplateCommand, ShiftTemplateDto>
{
    private readonly AppDbContext _db;
    private readonly IPermissionService _permissions;

    public CreateShiftTemplateCommandHandler(AppDbContext db, IPermissionService permissions)
    {
        _db = db;
        _permissions = permissions;
    }

    public async Task<ShiftTemplateDto> Handle(CreateShiftTemplateCommand req, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(
            req.RequestingUserId, req.SpaceId, Permissions.TasksManage, ct);

        // Verify group belongs to space
        var groupExists = await _db.Groups
            .AnyAsync(g => g.Id == req.GroupId && g.SpaceId == req.SpaceId && g.DeletedAt == null, ct);
        if (!groupExists)
            throw new KeyNotFoundException("Group not found.");

        // Verify group task belongs to the group and space
        var taskExists = await _db.GroupTasks
            .AnyAsync(t => t.Id == req.GroupTaskId && t.GroupId == req.GroupId && t.SpaceId == req.SpaceId && t.IsActive, ct);
        if (!taskExists)
            throw new KeyNotFoundException("Group task not found.");

        var template = ShiftTemplate.Create(
            req.SpaceId,
            req.GroupId,
            req.GroupTaskId,
            req.DayOfWeek,
            req.StartTime,
            req.EndTime,
            req.RequiredHeadcount,
            req.RequestingUserId);

        _db.ShiftTemplates.Add(template);
        await _db.SaveChangesAsync(ct);

        return ToDto(template);
    }

    private static ShiftTemplateDto ToDto(ShiftTemplate t) => new(
        t.Id, t.GroupId, t.GroupTaskId, t.DayOfWeek,
        t.StartTime, t.EndTime, t.RequiredHeadcount,
        t.IsDeleted, t.CreatedAt, t.UpdatedAt);
}

// ── Update ────────────────────────────────────────────────────────────────────

public record UpdateShiftTemplateCommand(
    Guid SpaceId,
    Guid GroupId,
    Guid TemplateId,
    Guid RequestingUserId,
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int RequiredHeadcount,
    Guid? GroupTaskId = null) : IRequest<ShiftTemplateDto>;

public class UpdateShiftTemplateCommandValidator : AbstractValidator<UpdateShiftTemplateCommand>
{
    public UpdateShiftTemplateCommandValidator()
    {
        RuleFor(x => x.SpaceId).NotEmpty();
        RuleFor(x => x.GroupId).NotEmpty();
        RuleFor(x => x.TemplateId).NotEmpty();
        RuleFor(x => x.RequestingUserId).NotEmpty();

        RuleFor(x => x.DayOfWeek).IsInEnum()
            .WithMessage("Day of week must be a valid value (Sunday through Saturday).");

        RuleFor(x => x.StartTime)
            .LessThan(x => x.EndTime)
            .WithMessage("Start time must be before end time.");

        RuleFor(x => x.RequiredHeadcount)
            .InclusiveBetween(1, 999)
            .WithMessage("Required headcount must be between 1 and 999.");

        RuleFor(x => x.GroupTaskId)
            .NotEqual(Guid.Empty)
            .When(x => x.GroupTaskId.HasValue)
            .WithMessage("Group task ID must not be empty when provided.");
    }
}

public class UpdateShiftTemplateCommandHandler : IRequestHandler<UpdateShiftTemplateCommand, ShiftTemplateDto>
{
    private readonly AppDbContext _db;
    private readonly IPermissionService _permissions;

    public UpdateShiftTemplateCommandHandler(AppDbContext db, IPermissionService permissions)
    {
        _db = db;
        _permissions = permissions;
    }

    public async Task<ShiftTemplateDto> Handle(UpdateShiftTemplateCommand req, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(
            req.RequestingUserId, req.SpaceId, Permissions.TasksManage, ct);

        var template = await _db.ShiftTemplates
            .FirstOrDefaultAsync(t => t.Id == req.TemplateId
                                   && t.GroupId == req.GroupId
                                   && t.SpaceId == req.SpaceId
                                   && !t.IsDeleted, ct)
            ?? throw new KeyNotFoundException("Shift template not found.");

        // If a new GroupTaskId is provided, verify it exists
        if (req.GroupTaskId.HasValue)
        {
            var taskExists = await _db.GroupTasks
                .AnyAsync(t => t.Id == req.GroupTaskId.Value
                            && t.GroupId == req.GroupId
                            && t.SpaceId == req.SpaceId
                            && t.IsActive, ct);
            if (!taskExists)
                throw new KeyNotFoundException("Group task not found.");
        }

        // Update the template
        template.Update(req.DayOfWeek, req.StartTime, req.EndTime, req.RequiredHeadcount, req.GroupTaskId);

        // Requirement 2.4: Apply changes only to future shift slots that have zero approved requests.
        // Slots with at least one approved request are preserved unchanged.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var futureSlotsFromTemplate = await _db.ShiftSlots
            .Where(s => s.ShiftTemplateId == req.TemplateId
                     && s.SpaceId == req.SpaceId
                     && s.Date > today)
            .ToListAsync(ct);

        // Get slot IDs that have at least one approved request (protected slots)
        var protectedSlotIds = await _db.ShiftRequests
            .Where(r => r.SpaceId == req.SpaceId
                     && r.Status == ShiftRequestStatus.Approved
                     && futureSlotsFromTemplate.Select(s => s.Id).Contains(r.ShiftSlotId))
            .Select(r => r.ShiftSlotId)
            .Distinct()
            .ToListAsync(ct);

        // Update only unprotected future slots to match new template values
        foreach (var slot in futureSlotsFromTemplate)
        {
            if (protectedSlotIds.Contains(slot.Id))
                continue;

            // Slot is unprotected — update its properties to match the new template
            slot.UpdateFromTemplate(req.StartTime, req.EndTime, req.RequiredHeadcount, req.GroupTaskId);
        }

        await _db.SaveChangesAsync(ct);

        return ToDto(template);
    }

    private static ShiftTemplateDto ToDto(ShiftTemplate t) => new(
        t.Id, t.GroupId, t.GroupTaskId, t.DayOfWeek,
        t.StartTime, t.EndTime, t.RequiredHeadcount,
        t.IsDeleted, t.CreatedAt, t.UpdatedAt);
}

// ── Delete (Soft-Delete) ──────────────────────────────────────────────────────

public record DeleteShiftTemplateCommand(
    Guid SpaceId,
    Guid GroupId,
    Guid TemplateId,
    Guid RequestingUserId) : IRequest;

public class DeleteShiftTemplateCommandValidator : AbstractValidator<DeleteShiftTemplateCommand>
{
    public DeleteShiftTemplateCommandValidator()
    {
        RuleFor(x => x.SpaceId).NotEmpty();
        RuleFor(x => x.GroupId).NotEmpty();
        RuleFor(x => x.TemplateId).NotEmpty();
        RuleFor(x => x.RequestingUserId).NotEmpty();
    }
}

public class DeleteShiftTemplateCommandHandler : IRequestHandler<DeleteShiftTemplateCommand>
{
    private readonly AppDbContext _db;
    private readonly IPermissionService _permissions;

    public DeleteShiftTemplateCommandHandler(AppDbContext db, IPermissionService permissions)
    {
        _db = db;
        _permissions = permissions;
    }

    public async Task Handle(DeleteShiftTemplateCommand req, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(
            req.RequestingUserId, req.SpaceId, Permissions.TasksManage, ct);

        var template = await _db.ShiftTemplates
            .FirstOrDefaultAsync(t => t.Id == req.TemplateId
                                   && t.GroupId == req.GroupId
                                   && t.SpaceId == req.SpaceId
                                   && !t.IsDeleted, ct)
            ?? throw new KeyNotFoundException("Shift template not found.");

        // Requirement 2.3: Soft-delete preserves already-generated slots with approved requests.
        // The template is marked as deleted so no future slots are generated from it.
        // Existing slots remain untouched regardless of their request status.
        template.SoftDelete();

        await _db.SaveChangesAsync(ct);
    }
}
