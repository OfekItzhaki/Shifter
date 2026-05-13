using Jobuler.Application.Common;
using Jobuler.Domain.Groups;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.HomeLeave.Commands;

public record UpsertHomeLeaveConfigCommand(
    Guid SpaceId,
    Guid GroupId,
    decimal MinRestHours,
    decimal EligibilityThresholdHours,
    int LeaveCapacity,
    decimal LeaveDurationHours,
    Guid RequestingUserId) : IRequest<HomeLeaveConfigResult>;

public record HomeLeaveConfigResult(
    Guid Id,
    Guid GroupId,
    Guid SpaceId,
    decimal MinRestHours,
    decimal EligibilityThresholdHours,
    int LeaveCapacity,
    decimal LeaveDurationHours);

public class UpsertHomeLeaveConfigCommandHandler : IRequestHandler<UpsertHomeLeaveConfigCommand, HomeLeaveConfigResult>
{
    private readonly AppDbContext _db;
    private readonly IPermissionService _permissions;

    public UpsertHomeLeaveConfigCommandHandler(AppDbContext db, IPermissionService permissions)
    {
        _db = db;
        _permissions = permissions;
    }

    public async Task<HomeLeaveConfigResult> Handle(UpsertHomeLeaveConfigCommand req, CancellationToken ct)
    {
        // Set PostgreSQL session variables for RLS policies.
        if (_db.Database.IsRelational())
        {
            await _db.Database.ExecuteSqlRawAsync(
                "SELECT set_config('app.current_space_id', {0}, TRUE), set_config('app.current_user_id', {1}, TRUE)",
                req.SpaceId.ToString(),
                req.RequestingUserId.ToString());
        }

        await _permissions.RequirePermissionAsync(req.RequestingUserId, req.SpaceId, Permissions.ConstraintsManage, ct);

        // Verify group exists and belongs to the space
        var group = await _db.Groups.AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == req.GroupId && g.SpaceId == req.SpaceId, ct)
            ?? throw new KeyNotFoundException("Group not found in this space.");

        // Validate leave_capacity against group member count
        var memberCount = await _db.GroupMemberships.AsNoTracking()
            .CountAsync(m => m.GroupId == req.GroupId && m.SpaceId == req.SpaceId, ct);

        if (memberCount > 0 && req.LeaveCapacity > memberCount - 1)
            throw new InvalidOperationException(
                $"leave_capacity must be between 1 and {memberCount - 1} (group member count minus 1).");

        // Load existing config or create new one
        var config = await _db.HomeLeaveConfigs
            .FirstOrDefaultAsync(c => c.GroupId == req.GroupId && c.SpaceId == req.SpaceId, ct);

        if (config is null)
        {
            config = HomeLeaveConfig.Create(
                req.SpaceId,
                req.GroupId,
                req.MinRestHours,
                req.EligibilityThresholdHours,
                req.LeaveCapacity,
                req.LeaveDurationHours);

            _db.HomeLeaveConfigs.Add(config);
        }
        else
        {
            config.Update(
                req.MinRestHours,
                req.EligibilityThresholdHours,
                req.LeaveCapacity,
                req.LeaveDurationHours);
        }

        await _db.SaveChangesAsync(ct);

        return new HomeLeaveConfigResult(
            config.Id,
            config.GroupId,
            config.SpaceId,
            config.MinRestHours,
            config.EligibilityThresholdHours,
            config.LeaveCapacity,
            config.LeaveDurationHours);
    }
}
