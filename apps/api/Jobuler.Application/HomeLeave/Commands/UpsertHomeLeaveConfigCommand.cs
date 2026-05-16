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
    Guid RequestingUserId,
    int? BalanceValue = null,
    HomeLeaveMode? Mode = null,
    int? BaseDays = null,
    int? HomeDays = null,
    int? SliderValue = null,
    bool? EmergencyFreezeActive = null,
    bool? EmergencyUseForScheduling = null,
    int MinPeopleAtBase = 8) : IRequest<HomeLeaveConfigResult>;

public record HomeLeaveConfigResult(
    Guid Id,
    Guid GroupId,
    Guid SpaceId,
    decimal MinRestHours,
    decimal EligibilityThresholdHours,
    int LeaveCapacity,
    decimal LeaveDurationHours,
    int BalanceValue,
    int MinPeopleAtBase,
    string Mode,
    int BaseDays,
    int HomeDays,
    bool EmergencyFreezeActive,
    bool EmergencyUseForScheduling,
    DateTime? FreezeStartedAt,
    FeasibilityResult? Feasibility,
    int? OptimalBaseDays,
    int? OptimalHomeDays,
    bool? OptimalIsReduced);

public class UpsertHomeLeaveConfigCommandHandler : IRequestHandler<UpsertHomeLeaveConfigCommand, HomeLeaveConfigResult>
{
    private readonly AppDbContext _db;
    private readonly IPermissionService _permissions;
    private readonly IOptimalRatioCalculator _optimalRatioCalculator;
    private readonly IFeasibilityEngine _feasibilityEngine;

    public UpsertHomeLeaveConfigCommandHandler(
        AppDbContext db,
        IPermissionService permissions,
        IOptimalRatioCalculator optimalRatioCalculator,
        IFeasibilityEngine feasibilityEngine)
    {
        _db = db;
        _permissions = permissions;
        _optimalRatioCalculator = optimalRatioCalculator;
        _feasibilityEngine = feasibilityEngine;
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

        // Get group member count for coverage calculations
        var memberCount = await _db.GroupMemberships.AsNoTracking()
            .CountAsync(m => m.GroupId == req.GroupId && m.SpaceId == req.SpaceId, ct);

        // Derive leaveCapacity from minPeopleAtBase: leaveCapacity = memberCount - minPeopleAtBase
        var minPeopleAtBase = req.MinPeopleAtBase;
        if (minPeopleAtBase >= memberCount && memberCount > 1)
            throw new InvalidOperationException(
                $"min_people_at_base must be less than the group member count ({memberCount}).");

        var leaveCapacity = Math.Max(1, memberCount - minPeopleAtBase);

        // Determine the effective mode (default to Automatic for backward compatibility)
        var effectiveMode = req.Mode ?? HomeLeaveMode.Automatic;

        // Compute optimal ratio when in Automatic mode
        OptimalRatioResult? optimalRatio = null;
        if (effectiveMode == HomeLeaveMode.Automatic && memberCount >= 2)
        {
            optimalRatio = _optimalRatioCalculator.Calculate(
                memberCount,
                minPeopleAtBase,
                req.LeaveDurationHours);
        }

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
                leaveCapacity,
                req.LeaveDurationHours,
                req.BalanceValue ?? 50,
                effectiveMode,
                req.BaseDays ?? optimalRatio?.BaseDays ?? 7,
                req.HomeDays ?? optimalRatio?.HomeDays ?? 2,
                minPeopleAtBase);

            _db.HomeLeaveConfigs.Add(config);
        }
        else
        {
            // Update base fields
            config.Update(
                req.MinRestHours,
                req.EligibilityThresholdHours,
                leaveCapacity,
                req.LeaveDurationHours,
                req.BalanceValue,
                minPeopleAtBase: minPeopleAtBase);

            // Handle mode change
            if (config.Mode != effectiveMode)
            {
                config.SetMode(effectiveMode);
            }

            // Apply mode-specific logic
            if (effectiveMode == HomeLeaveMode.Automatic && req.SliderValue.HasValue && optimalRatio is not null)
            {
                config.SetSliderPosition(req.SliderValue.Value, optimalRatio.BaseDays, optimalRatio.HomeDays);
            }
            else if (effectiveMode == HomeLeaveMode.Manual && req.BaseDays.HasValue && req.HomeDays.HasValue)
            {
                config.SetRatio(req.BaseDays.Value, req.HomeDays.Value);
            }

            // Handle emergency freeze state changes
            if (req.EmergencyFreezeActive == true && !config.EmergencyFreezeActive)
            {
                config.ActivateEmergencyFreeze(req.EmergencyUseForScheduling ?? false);
            }
            else if (req.EmergencyFreezeActive == false && config.EmergencyFreezeActive)
            {
                config.DeactivateEmergencyFreeze();
            }
        }

        await _db.SaveChangesAsync(ct);

        // Compute feasibility for the current configuration
        FeasibilityResult? feasibility = null;
        if (memberCount >= 2 && !config.EmergencyFreezeActive)
        {
            feasibility = _feasibilityEngine.Evaluate(
                memberCount,
                config.MinPeopleAtBase,
                config.BaseDays,
                config.HomeDays);
        }

        return new HomeLeaveConfigResult(
            config.Id,
            config.GroupId,
            config.SpaceId,
            config.MinRestHours,
            config.EligibilityThresholdHours,
            config.LeaveCapacity,
            config.LeaveDurationHours,
            config.BalanceValue,
            config.MinPeopleAtBase,
            config.Mode.ToString().ToLowerInvariant(),
            config.BaseDays,
            config.HomeDays,
            config.EmergencyFreezeActive,
            config.EmergencyUseForScheduling,
            config.FreezeStartedAt,
            feasibility,
            optimalRatio?.BaseDays,
            optimalRatio?.HomeDays,
            optimalRatio?.IsReduced);
    }
}
