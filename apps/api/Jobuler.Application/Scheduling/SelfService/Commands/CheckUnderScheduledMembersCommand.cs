using FluentValidation;
using Jobuler.Application.Notifications;
using Jobuler.Domain.Notifications;
using Jobuler.Domain.Scheduling;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Jobuler.Application.Scheduling.SelfService.Commands;

/// <summary>
/// Triggered on request window close to detect members with fewer approved shifts
/// than MinShiftsPerCycle. Marks them as under-scheduled and sends notifications
/// to both the member and the group admin.
/// Requirements: 5.4, 6.7
/// </summary>
public record CheckUnderScheduledMembersCommand(
    Guid SpaceId,
    Guid GroupId,
    Guid SchedulingCycleId) : IRequest<CheckUnderScheduledMembersResult>;

public record UnderScheduledMemberInfo(
    Guid PersonId,
    string PersonName,
    int ApprovedCount,
    int MinRequired);

public record CheckUnderScheduledMembersResult(
    bool Success,
    IReadOnlyList<UnderScheduledMemberInfo> UnderScheduledMembers);

public class CheckUnderScheduledMembersCommandValidator : AbstractValidator<CheckUnderScheduledMembersCommand>
{
    public CheckUnderScheduledMembersCommandValidator()
    {
        RuleFor(x => x.SpaceId).NotEmpty().WithMessage("SpaceId is required.");
        RuleFor(x => x.GroupId).NotEmpty().WithMessage("GroupId is required.");
        RuleFor(x => x.SchedulingCycleId).NotEmpty().WithMessage("SchedulingCycleId is required.");
    }
}

public class CheckUnderScheduledMembersCommandHandler
    : IRequestHandler<CheckUnderScheduledMembersCommand, CheckUnderScheduledMembersResult>
{
    private readonly AppDbContext _db;
    private readonly INotificationService _notificationService;
    private readonly IPushNotificationSender _pushSender;
    private readonly ILogger<CheckUnderScheduledMembersCommandHandler> _logger;

    public CheckUnderScheduledMembersCommandHandler(
        AppDbContext db,
        INotificationService notificationService,
        IPushNotificationSender pushSender,
        ILogger<CheckUnderScheduledMembersCommandHandler> logger)
    {
        _db = db;
        _notificationService = notificationService;
        _pushSender = pushSender;
        _logger = logger;
    }

    public async Task<CheckUnderScheduledMembersResult> Handle(
        CheckUnderScheduledMembersCommand request, CancellationToken ct)
    {
        // Load the scheduling cycle
        var cycle = await _db.SchedulingCycles
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.SchedulingCycleId
                                      && c.SpaceId == request.SpaceId
                                      && c.GroupId == request.GroupId, ct);

        if (cycle is null)
            throw new KeyNotFoundException("Scheduling cycle not found.");

        // Load the self-service config to get MinShiftsPerCycle
        var config = await _db.SelfServiceConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.GroupId == request.GroupId
                                      && c.SpaceId == request.SpaceId, ct);

        if (config is null)
        {
            _logger.LogWarning(
                "No SelfServiceConfig found for group {GroupId} in space {SpaceId}. Skipping under-scheduled check.",
                request.GroupId, request.SpaceId);
            return new CheckUnderScheduledMembersResult(true, []);
        }

        var minShifts = config.MinShiftsPerCycle;

        // If MinShiftsPerCycle is 0, no one can be under-scheduled
        if (minShifts == 0)
        {
            _logger.LogInformation(
                "MinShiftsPerCycle is 0 for group {GroupId}. No under-scheduled detection needed.",
                request.GroupId);
            return new CheckUnderScheduledMembersResult(true, []);
        }

        // Get all members of the group
        var groupMembers = await _db.GroupMemberships
            .AsNoTracking()
            .Where(gm => gm.GroupId == request.GroupId && gm.SpaceId == request.SpaceId)
            .Select(gm => gm.PersonId)
            .ToListAsync(ct);

        if (groupMembers.Count == 0)
        {
            _logger.LogInformation(
                "No members in group {GroupId}. Skipping under-scheduled check.",
                request.GroupId);
            return new CheckUnderScheduledMembersResult(true, []);
        }

        // Count approved shift requests per member for this cycle
        var approvedCounts = await _db.ShiftRequests
            .AsNoTracking()
            .Where(sr => sr.GroupId == request.GroupId
                         && sr.SpaceId == request.SpaceId
                         && sr.SchedulingCycleId == request.SchedulingCycleId
                         && sr.Status == ShiftRequestStatus.Approved)
            .GroupBy(sr => sr.PersonId)
            .Select(g => new { PersonId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var approvedCountMap = approvedCounts.ToDictionary(x => x.PersonId, x => x.Count);

        // Identify under-scheduled members (approved count < MinShiftsPerCycle)
        var underScheduledPersonIds = groupMembers
            .Where(personId =>
            {
                var count = approvedCountMap.GetValueOrDefault(personId, 0);
                return count < minShifts;
            })
            .ToList();

        if (underScheduledPersonIds.Count == 0)
        {
            _logger.LogInformation(
                "All members in group {GroupId} meet MinShiftsPerCycle ({MinShifts}) for cycle {CycleId}.",
                request.GroupId, minShifts, request.SchedulingCycleId);
            return new CheckUnderScheduledMembersResult(true, []);
        }

        // Load person names for notification content
        var persons = await _db.People
            .AsNoTracking()
            .Where(p => underScheduledPersonIds.Contains(p.Id) && p.SpaceId == request.SpaceId)
            .Select(p => new { p.Id, p.FullName, p.LinkedUserId })
            .ToListAsync(ct);

        var underScheduledMembers = persons.Select(p => new UnderScheduledMemberInfo(
            PersonId: p.Id,
            PersonName: p.FullName,
            ApprovedCount: approvedCountMap.GetValueOrDefault(p.Id, 0),
            MinRequired: minShifts
        )).ToList();

        _logger.LogInformation(
            "Detected {Count} under-scheduled members in group {GroupId} for cycle {CycleId}. MinShifts={MinShifts}.",
            underScheduledMembers.Count, request.GroupId, request.SchedulingCycleId, minShifts);

        // Req 6.7 / 13.6: Send notification to group admin listing under-scheduled members
        var memberSummary = string.Join(", ",
            underScheduledMembers.Select(m => $"{m.PersonName} ({m.ApprovedCount}/{m.MinRequired})"));

        var adminMetadata = JsonSerializer.Serialize(new
        {
            groupId = request.GroupId,
            schedulingCycleId = request.SchedulingCycleId,
            underScheduledMembers = underScheduledMembers.Select(m => new
            {
                personId = m.PersonId,
                personName = m.PersonName,
                approvedCount = m.ApprovedCount,
                minRequired = m.MinRequired
            })
        });

        await _notificationService.NotifySpaceAdminsAsync(
            request.SpaceId,
            eventType: "self_service.under_scheduled_members",
            title: "Under-Scheduled Members Detected",
            body: $"{underScheduledMembers.Count} member(s) have fewer shifts than the minimum ({minShifts}): {memberSummary}",
            metadataJson: adminMetadata,
            groupId: request.GroupId,
            ct: ct);

        // Req 5.4 / 13.6: Send notification to each under-scheduled member
        var memberUserIds = persons
            .Where(p => p.LinkedUserId.HasValue)
            .ToList();

        foreach (var person in memberUserIds)
        {
            var shortfall = minShifts - approvedCountMap.GetValueOrDefault(person.Id, 0);

            var memberNotification = Notification.Create(
                spaceId: request.SpaceId,
                userId: person.LinkedUserId!.Value,
                eventType: "self_service.under_scheduled_warning",
                title: "Under-Scheduled Warning",
                body: $"You have {approvedCountMap.GetValueOrDefault(person.Id, 0)} approved shift(s) for this cycle, " +
                      $"which is {shortfall} below the minimum of {minShifts}. Please request additional shifts.",
                metadataJson: JsonSerializer.Serialize(new
                {
                    groupId = request.GroupId,
                    schedulingCycleId = request.SchedulingCycleId,
                    approvedCount = approvedCountMap.GetValueOrDefault(person.Id, 0),
                    minRequired = minShifts,
                    shortfall
                }));

            _db.Notifications.Add(memberNotification);
        }

        await _db.SaveChangesAsync(ct);

        // Attempt push notifications to under-scheduled members (failures don't affect in-app)
        try
        {
            var userIdsForPush = memberUserIds
                .Select(p => p.LinkedUserId!.Value)
                .ToList();

            if (userIdsForPush.Count > 0)
            {
                var pushPayload = new PushPayload(
                    Title: "Under-Scheduled Warning",
                    Body: $"You have fewer shifts than the minimum ({minShifts}) for this cycle. Please request additional shifts.",
                    Icon: "/favicon.jpeg",
                    Url: "/shifts");

                await _pushSender.SendPushToUsersAsync(userIdsForPush, request.SpaceId, pushPayload, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Push notification delivery failed for under-scheduled members in group {GroupId}. " +
                "In-app notifications were persisted successfully.",
                request.GroupId);
        }

        return new CheckUnderScheduledMembersResult(
            Success: true,
            UnderScheduledMembers: underScheduledMembers);
    }
}
