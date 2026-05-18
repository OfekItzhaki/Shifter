using Jobuler.Application.Common;
using Jobuler.Application.Scheduling.Models;
using Jobuler.Domain.Scheduling;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Scheduling.Queries;

public record GetRecommendationsForRunQuery(
    Guid SpaceId,
    Guid RunId,
    Guid UserId) : IRequest<RecommendationBannerDto?>;

public class GetRecommendationsForRunQueryHandler : IRequestHandler<GetRecommendationsForRunQuery, RecommendationBannerDto?>
{
    private readonly AppDbContext _db;
    private readonly IPermissionService _permissions;
    private const int MaxBannerRecommendations = 5;

    public GetRecommendationsForRunQueryHandler(AppDbContext db, IPermissionService permissions)
    {
        _db = db;
        _permissions = permissions;
    }

    public async Task<RecommendationBannerDto?> Handle(GetRecommendationsForRunQuery req, CancellationToken ct)
    {
        // Permission check: return empty if user doesn't have TasksManage permission (ViewAndEdit or Owner)
        var hasPermission = await _permissions.HasPermissionAsync(req.UserId, req.SpaceId, Permissions.TasksManage, ct);
        if (!hasPermission)
            return null;

        // Load all recommendations for this run (any status — we filter to Active below)
        var recommendations = await _db.DoubleShiftRecommendations.AsNoTracking()
            .Where(r => r.SpaceId == req.SpaceId
                     && r.ScheduleRunId == req.RunId
                     && r.Status == RecommendationStatus.Active)
            .OrderByDescending(r => r.AdditionalSlotsCovered)
            .ThenBy(r => r.TaskName)
            .ToListAsync(ct);

        if (recommendations.Count == 0)
            return null;

        // Determine the GroupId from the first recommendation to check EmergencyFreezeActive
        var groupId = recommendations[0].GroupId;

        // Check EmergencyFreezeActive — return empty if true (Req 6.3)
        var homeLeaveConfig = await _db.HomeLeaveConfigs.AsNoTracking()
            .FirstOrDefaultAsync(c => c.SpaceId == req.SpaceId && c.GroupId == groupId, ct);

        if (homeLeaveConfig?.EmergencyFreezeActive == true)
            return null;

        // Build the banner DTO
        var totalUncoveredSlots = recommendations[0].TotalUncoveredSlotsInRun;

        var bannerRecommendations = recommendations
            .Take(MaxBannerRecommendations)
            .Select(r => new RecommendationDto(
                r.Id,
                r.GroupTaskId,
                r.TaskName,
                r.Status.ToString(),
                r.AdditionalSlotsCovered,
                r.AffectedDateStart,
                r.AffectedDateEnd,
                r.TotalUncoveredSlotsInRun,
                r.CreatedAt))
            .ToList();

        var remainingCount = Math.Max(0, recommendations.Count - MaxBannerRecommendations);

        // AffectedDateRange: earliest start to latest end across all recommendations
        var earliestStart = recommendations.Min(r => r.AffectedDateStart);
        var latestEnd = recommendations.Max(r => r.AffectedDateEnd);
        var affectedDateRange = $"{earliestStart:dd/MM/yyyy} - {latestEnd:dd/MM/yyyy}";

        return new RecommendationBannerDto(
            totalUncoveredSlots,
            bannerRecommendations,
            remainingCount,
            affectedDateRange);
    }
}
