using FluentValidation;
using Jobuler.Application.Notifications;
using Jobuler.Application.Scheduling.SelfService.Models;
using Jobuler.Domain.Notifications;
using Jobuler.Domain.Scheduling;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Jobuler.Application.Scheduling.SelfService.Commands;

/// <summary>
/// Command to accept a waitlist offer for a shift slot.
/// Processes the acceptance as a standard shift request (subject to Max_Shifts validation).
/// If Max_Shifts validation fails, removes the member from the waitlist and cascades to the next member.
/// </summary>
public record AcceptWaitlistOfferCommand(
    Guid SpaceId,
    Guid PersonId,
    Guid ShiftSlotId) : IRequest<AcceptWaitlistOfferResult>;

public record AcceptWaitlistOfferResult(
    bool Success,
    Guid? ShiftRequestId,
    string? ErrorMessage);

public class AcceptWaitlistOfferCommandValidator : AbstractValidator<AcceptWaitlistOfferCommand>
{
    public AcceptWaitlistOfferCommandValidator()
    {
        RuleFor(x => x.SpaceId).NotEmpty().WithMessage("SpaceId is required.");
        RuleFor(x => x.PersonId).NotEmpty().WithMessage("PersonId is required.");
        RuleFor(x => x.ShiftSlotId).NotEmpty().WithMessage("ShiftSlotId is required.");
    }
}

public class AcceptWaitlistOfferCommandHandler : IRequestHandler<AcceptWaitlistOfferCommand, AcceptWaitlistOfferResult>
{
    private readonly AppDbContext _db;
    private readonly IWaitlistService _waitlistService;
    private readonly IPushNotificationSender _pushSender;
    private readonly INotificationService _notificationService;
    private readonly ILogger<AcceptWaitlistOfferCommandHandler> _logger;

    public AcceptWaitlistOfferCommandHandler(
        AppDbContext db,
        IWaitlistService waitlistService,
        IPushNotificationSender pushSender,
        INotificationService notificationService,
        ILogger<AcceptWaitlistOfferCommandHandler> logger)
    {
        _db = db;
        _waitlistService = waitlistService;
        _pushSender = pushSender;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<AcceptWaitlistOfferResult> Handle(AcceptWaitlistOfferCommand request, CancellationToken ct)
    {
        // Find the member's offered waitlist entry for this slot
        var entry = await _db.WaitlistEntries
            .FirstOrDefaultAsync(e => e.ShiftSlotId == request.ShiftSlotId
                                      && e.PersonId == request.PersonId
                                      && e.Status == WaitlistEntryStatus.Offered, ct);

        if (entry is null)
        {
            return new AcceptWaitlistOfferResult(
                Success: false,
                ShiftRequestId: null,
                ErrorMessage: "No active offer found for this slot.");
        }

        // Check if the offer has expired
        if (entry.ExpiresAt.HasValue && entry.ExpiresAt.Value < DateTime.UtcNow)
        {
            entry.Expire();
            await _db.SaveChangesAsync(ct);

            // Cascade to next waiting member
            await _waitlistService.ProcessSlotReleasedAsync(request.ShiftSlotId, ct);

            return new AcceptWaitlistOfferResult(
                Success: false,
                ShiftRequestId: null,
                ErrorMessage: "The offer has expired.");
        }

        // Load the slot
        var slot = await _db.ShiftSlots
            .FirstOrDefaultAsync(s => s.Id == request.ShiftSlotId && s.SpaceId == request.SpaceId, ct);

        if (slot is null)
        {
            return new AcceptWaitlistOfferResult(
                Success: false,
                ShiftRequestId: null,
                ErrorMessage: "The shift slot does not exist.");
        }

        // Req 9.5: Validate Max_Shifts constraint before accepting
        var config = await _db.SelfServiceConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.GroupId == slot.GroupId, ct);

        var maxShifts = config?.MaxShiftsPerCycle ?? 7;

        var currentShiftCount = await _db.ShiftRequests
            .CountAsync(r => r.PersonId == request.PersonId
                             && r.SchedulingCycleId == slot.SchedulingCycleId
                             && (r.Status == ShiftRequestStatus.Pending || r.Status == ShiftRequestStatus.Approved), ct);

        if (currentShiftCount >= maxShifts)
        {
            // Req 9.5: Remove from waitlist and offer to next member
            entry.Remove();
            await _db.SaveChangesAsync(ct);

            await _waitlistService.ProcessSlotReleasedAsync(request.ShiftSlotId, ct);

            _logger.LogInformation(
                "Person {PersonId} failed Max_Shifts validation on waitlist acceptance for slot {SlotId}. " +
                "Removed from waitlist and cascading to next member.",
                request.PersonId, request.ShiftSlotId);

            return new AcceptWaitlistOfferResult(
                Success: false,
                ShiftRequestId: null,
                ErrorMessage: $"You have reached the maximum number of shifts ({maxShifts}) for this scheduling cycle. " +
                              "You have been removed from the waitlist.");
        }

        // All validations passed — accept the offer and create a shift request
        entry.Accept();

        var shiftRequest = ShiftRequest.Create(
            spaceId: slot.SpaceId,
            shiftSlotId: slot.Id,
            personId: request.PersonId,
            groupId: slot.GroupId,
            schedulingCycleId: slot.SchedulingCycleId);

        shiftRequest.Approve();
        slot.IncrementFillCount();

        _db.ShiftRequests.Add(shiftRequest);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Person {PersonId} accepted waitlist offer for slot {SlotId}. Request {RequestId} approved.",
            request.PersonId, request.ShiftSlotId, shiftRequest.Id);

        // Req 13.1: Send notification for approved request (via waitlist acceptance)
        await SendWaitlistAcceptedNotificationAsync(request.PersonId, slot, shiftRequest.Id, ct);
        await NotifyAdminsWaitlistAcceptedAsync(request.PersonId, slot, shiftRequest.Id, ct);

        return new AcceptWaitlistOfferResult(
            Success: true,
            ShiftRequestId: shiftRequest.Id,
            ErrorMessage: null);
    }

    /// <summary>
    /// Sends an in-app and push notification when a waitlist offer is accepted and the request is approved (Req 13.1).
    /// Push failures are logged but do not affect in-app notification persistence (Req 13.7).
    /// </summary>
    private async Task SendWaitlistAcceptedNotificationAsync(
        Guid personId, ShiftSlot slot, Guid shiftRequestId, CancellationToken ct)
    {
        try
        {
            var person = await _db.People
                .AsNoTracking()
                .Where(p => p.Id == personId && p.SpaceId == slot.SpaceId)
                .Select(p => new { p.LinkedUserId })
                .FirstOrDefaultAsync(ct);

            if (person?.LinkedUserId is null)
                return;

            var taskName = await _db.GroupTasks
                .AsNoTracking()
                .Where(t => t.Id == slot.GroupTaskId)
                .Select(t => t.Name)
                .FirstOrDefaultAsync(ct) ?? "Shift";

            var title = "Shift Request Approved";
            var body = $"Your waitlist offer for {taskName} on {slot.Date:MMM dd} ({slot.StartTime:HH:mm}–{slot.EndTime:HH:mm}) " +
                       "has been accepted and approved.";

            var notification = Notification.Create(
                spaceId: slot.SpaceId,
                userId: person.LinkedUserId.Value,
                eventType: "self_service.request_approved",
                title: title,
                body: body,
                metadataJson: JsonSerializer.Serialize(new
                {
                    shiftRequestId,
                    shiftSlotId = slot.Id,
                    groupId = slot.GroupId,
                    date = slot.Date,
                    startTime = slot.StartTime.ToString("HH:mm"),
                    endTime = slot.EndTime.ToString("HH:mm"),
                    taskName,
                    source = "waitlist_acceptance"
                }));

            _db.Notifications.Add(notification);
            await _db.SaveChangesAsync(ct);

            // Attempt push notification — failure does not affect in-app (Req 13.7)
            try
            {
                var payload = new PushPayload(
                    Title: title,
                    Body: body,
                    Icon: "/favicon.jpeg",
                    Url: "/shifts");

                await _pushSender.SendPushToUserAsync(person.LinkedUserId.Value, slot.SpaceId, payload, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Push notification delivery failed for waitlist accepted (person {PersonId}, slot {SlotId}). " +
                    "In-app notification was persisted successfully.",
                    personId, slot.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send waitlist accepted notification for person {PersonId}, slot {SlotId}",
                personId, slot.Id);
        }
    }

    private async Task NotifyAdminsWaitlistAcceptedAsync(
        Guid personId, ShiftSlot slot, Guid shiftRequestId, CancellationToken ct)
    {
        try
        {
            var personName = await _db.People
                .AsNoTracking()
                .Where(p => p.Id == personId && p.SpaceId == slot.SpaceId)
                .Select(p => p.DisplayName ?? p.FullName)
                .FirstOrDefaultAsync(ct) ?? "Member";

            var taskName = await _db.GroupTasks
                .AsNoTracking()
                .Where(t => t.Id == slot.GroupTaskId)
                .Select(t => t.Name)
                .FirstOrDefaultAsync(ct) ?? "Shift";

            await _notificationService.NotifySpaceAdminsAsync(
                slot.SpaceId,
                "self_service.waitlist_accepted",
                "Waitlist Offer Accepted",
                $"{personName} accepted a waitlist offer for {taskName} on {slot.Date:MMM dd} ({slot.StartTime:HH:mm}-{slot.EndTime:HH:mm}).",
                JsonSerializer.Serialize(new
                {
                    shiftRequestId,
                    shiftSlotId = slot.Id,
                    personId,
                    personName,
                    groupId = slot.GroupId,
                    schedulingCycleId = slot.SchedulingCycleId,
                    date = slot.Date,
                    startTime = slot.StartTime.ToString("HH:mm"),
                    endTime = slot.EndTime.ToString("HH:mm"),
                    taskName
                }),
                groupId: slot.GroupId,
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to notify admins about waitlist acceptance for person {PersonId}, slot {SlotId}",
                personId, slot.Id);
        }
    }
}
