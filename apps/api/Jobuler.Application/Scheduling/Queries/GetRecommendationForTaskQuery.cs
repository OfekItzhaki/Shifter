using Jobuler.Application.Common;
using Jobuler.Application.Scheduling.Models;
using Jobuler.Domain.Scheduling;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Scheduling.Queries;

public record GetRecommendationForTaskQuery(
    Guid SpaceId,
    Guid GroupTaskId,
    Guid UserId) : IRequest<RecommendationDto?>;

public class GetRecommendationForTaskQueryHandler : IRequestHandler<GetRecommendationForTaskQuery, RecommendationDto?>
{
    private readonly AppDbContext _db;
    private readonly IPermissionService _permissions;

    public GetRecommendationForTaskQueryHandler(AppDbContext db, IPermissionService permissions)
    {
        _db = db;
        _permissions = permissions;
    }

    public async Task<RecommendationDto?> Handle(GetRecommendationForTaskQuery req, CancellationToken ct)
    {
        // Requirement 6.1: Only ViewAndEdit or Owner users see recommendations
        var hasPermission = await _permissions.HasPermissionAsync(req.UserId, req.SpaceId, Permissions.TasksManage, ct);
        if (!hasPermission)
            return null;

        // Requirement 6.2: Do not display recommendations for tasks where AllowsDoubleShift is already true
        var task = await _db.GroupTasks.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == req.GroupTaskId && t.SpaceId == req.SpaceId, ct);

        if (task is null || task.AllowsDoubleShift)
            return null;

        // Requirement 6.3: Suppress recommendations when EmergencyFreezeActive is true
        var homeLeaveConfig = await _db.HomeLeaveConfigs.AsNoTracking()
            .FirstOrDefaultAsync(c => c.SpaceId == req.SpaceId && c.GroupId == task.GroupId, ct);

        if (homeLeaveConfig?.EmergencyFreezeActive == true)
            return null;

        // Query the most recent active recommendation for this task
        var recommendation = await _db.DoubleShiftRecommendations.AsNoTracking()
            .Where(r => r.SpaceId == req.SpaceId
                     && r.GroupTaskId == req.GroupTaskId
                     && r.Status == RecommendationStatus.Active)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (recommendation is null)
            return null;

        return new RecommendationDto(
            recommendation.Id,
            recommendation.GroupTaskId,
            recommendation.TaskName,
            recommendation.Status.ToString(),
            recommendation.AdditionalSlotsCovered,
            recommendation.AffectedDateStart,
            recommendation.AffectedDateEnd,
            recommendation.TotalUncoveredSlotsInRun,
            recommendation.CreatedAt);
    }
}
