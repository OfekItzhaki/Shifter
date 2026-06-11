using Jobuler.Application.Notifications;
using Jobuler.Application.Scheduling.SelfService.Models;
using Jobuler.Domain.Notifications;
using Jobuler.Domain.Scheduling;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Jobuler.Application.Scheduling.SelfService;

/// <summary>
/// Manages the waitlist for full shift slots.
/// Handles joining, leaving, slot release cascading, and expired offer processing.
/// Uses FIFO ordering (first-come, first-served) based on position.
/// </summary>
public class WaitlistService : IWaitlistService
{
    private readonly AppDbContext _db;
    private readonly IPushNotificationSender _pushSender;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<WaitlistService> _logger;

    public WaitlistService(
        AppDbContext db,
        IPushNotificationSender pushSender,
        TimeProvider timeProvider,
        ILogger<WaitlistService> logger)
    {
        _db = db;
        _pushSender = pushSender;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<WaitlistResult> JoinWaitlistAsync(Guid personId, Guid shiftSlotId, CancellationToken ct = default)
    {
        // Req 9.7: Prevent duplicate waitlist entries (Waiting or Offered status)
        var hasDuplicate = await _db.WaitlistEntries
            .AnyAsync(e => e.ShiftSlotId == shiftSlotId
                           && e.PersonId == personId
                           && (e.Status == WaitlistEntryStatus.Waiting || e.Status == WaitlistEntryStatus.Offered), ct);

        if (hasDuplicate)
        {
            return new WaitlistResult(
                Success: false,
                Position: null,
                ErrorMessage: "You are already on the waitlist for this slot.");
        }

        var slot = await _db.ShiftSlots
            .FirstOrDefaultAsync(s => s.Id == shiftSlotId, ct);

        if (slot is null)
        {
            return new WaitlistResult(
                Success: false,
                Position: null,
                ErrorMessage: "The requested shift slot does not exist.");
        }

        if (slot.HasAvailableCapacity())
        {
            return new WaitlistResult(
                Success: false,
                Position: null,
                ErrorMessage: "This shift still has available capacity. Request the shift directly instead.");
        }

        var cycle = await _db.SchedulingCycles
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == slot.SchedulingCycleId, ct);

        if (cycle is null)
        {
            return new WaitlistResult(
                Success: false,
                Position: null,
                ErrorMessage: "The scheduling cycle for this slot could not be found.");
        }

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        if (!cycle.IsRequestWindowOpen(utcNow))
        {
            return new WaitlistResult(
                Success: false,
                Position: null,
                ErrorMessage: "The request window has closed.");
        }

        var hasActiveShiftRequest = await _db.ShiftRequests
            .AnyAsync(r => r.ShiftSlotId == shiftSlotId
                           && r.PersonId == personId
                           && (r.Status == ShiftRequestStatus.Pending || r.Status == ShiftRequestStatus.Approved), ct);

        if (hasActiveShiftRequest)
        {
            return new WaitlistResult(
                Success: false,
                Position: null,
                ErrorMessage: "You already have an active request for this slot.");
        }

        // Determine next position (max position + 1 among active entries for this slot)
        var maxPosition = await _db.WaitlistEntries
            .Where(e => e.ShiftSlotId == shiftSlotId
                        && (e.Status == WaitlistEntryStatus.Waiting || e.Status == WaitlistEntryStatus.Offered))
            .MaxAsync(e => (int?)e.Position, ct) ?? 0;

        var nextPosition = maxPosition + 1;

        var entry = WaitlistEntry.Create(
            spaceId: slot.SpaceId,
            shiftSlotId: shiftSlotId,
            personId: personId,
            position: nextPosition);

        _db.WaitlistEntries.Add(entry);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Person {PersonId} joined waitlist for slot {SlotId} at position {Position}",
            personId, shiftSlotId, nextPosition);

        return new WaitlistResult(
            Success: true,
            Position: nextPosition,
            ErrorMessage: null);
    }

    /// <inheritdoc />
    public async Task LeaveWaitlistAsync(Guid personId, Guid shiftSlotId, CancellationToken ct = default)
    {
        // Find the member's active waitlist entry (Waiting or Offered)
        var entry = await _db.WaitlistEntries
            .FirstOrDefaultAsync(e => e.ShiftSlotId == shiftSlotId
                                      && e.PersonId == personId
                                      && (e.Status == WaitlistEntryStatus.Waiting || e.Status == WaitlistEntryStatus.Offered), ct);

        if (entry is null)
        {
            _logger.LogDebug(
                "Person {PersonId} attempted to leave waitlist for slot {SlotId} but has no active entry.",
                personId, shiftSlotId);
            return;
        }

        var hadActiveOffer = entry.Status == WaitlistEntryStatus.Offered;

        // Req 9.6: If the member has an active offer, treat removal as a decline
        if (hadActiveOffer)
        {
            entry.Decline();
        }
        else
        {
            entry.Remove();
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Person {PersonId} left waitlist for slot {SlotId}. Had active offer: {HadOffer}",
            personId, shiftSlotId, hadActiveOffer);

        // Req 9.6: If member had an active offer, cascade to next waiting member
        if (hadActiveOffer)
        {
            await OfferToNextWaitingMemberAsync(shiftSlotId, ct);
        }
    }

    /// <inheritdoc />
    public async Task ProcessSlotReleasedAsync(Guid shiftSlotId, CancellationToken ct = default)
    {
        // Check if there's already an active offer for this slot
        var hasActiveOffer = await _db.WaitlistEntries
            .AnyAsync(e => e.ShiftSlotId == shiftSlotId
                           && e.Status == WaitlistEntryStatus.Offered, ct);

        if (hasActiveOffer)
        {
            _logger.LogDebug(
                "Slot {SlotId} already has an active offer. Skipping slot release processing.",
                shiftSlotId);
            return;
        }

        // Offer to the first waiting member
        await OfferToNextWaitingMemberAsync(shiftSlotId, ct);
    }

    /// <inheritdoc />
    public async Task ProcessExpiredOffersAsync(CancellationToken ct = default)
    {
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

        // Find all expired offers (Offered status with ExpiresAt in the past)
        var expiredEntries = await _db.WaitlistEntries
            .Where(e => e.Status == WaitlistEntryStatus.Offered
                        && e.ExpiresAt != null
                        && e.ExpiresAt < utcNow)
            .ToListAsync(ct);

        if (expiredEntries.Count == 0)
            return;

        _logger.LogInformation(
            "Processing {Count} expired waitlist offers.", expiredEntries.Count);

        var expiredNotifications = await BuildWaitlistOfferExpiredNotificationsAsync(expiredEntries, ct);

        // Group by slot to cascade offers per slot
        var slotIds = expiredEntries.Select(e => e.ShiftSlotId).Distinct().ToList();

        foreach (var entry in expiredEntries)
        {
            entry.Expire();
        }

        _db.Notifications.AddRange(expiredNotifications.Select(n => n.Notification));
        await _db.SaveChangesAsync(ct);

        foreach (var notification in expiredNotifications)
        {
            await TrySendWaitlistOfferExpiredPushAsync(notification, ct);
        }

        // Cascade to next waiting member for each affected slot
        foreach (var slotId in slotIds)
        {
            await OfferToNextWaitingMemberAsync(slotId, ct);
        }
    }

    /// <summary>
    /// Offers the slot to the next waiting member in FIFO order.
    /// Sets ExpiresAt based on the group's WaitlistOfferMinutes configuration.
    /// If no waiting members remain, the slot stays open with available capacity.
    /// </summary>
    private async Task OfferToNextWaitingMemberAsync(Guid shiftSlotId, CancellationToken ct)
    {
        // Find the next waiting entry by lowest position
        var nextEntry = await _db.WaitlistEntries
            .Where(e => e.ShiftSlotId == shiftSlotId
                        && e.Status == WaitlistEntryStatus.Waiting)
            .OrderBy(e => e.Position)
            .FirstOrDefaultAsync(ct);

        if (nextEntry is null)
        {
            _logger.LogDebug(
                "No waiting members on waitlist for slot {SlotId}. Slot remains open.",
                shiftSlotId);
            return;
        }

        // Load the slot to get GroupId for config lookup
        var slot = await _db.ShiftSlots
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == shiftSlotId, ct);

        if (slot is null)
        {
            _logger.LogWarning(
                "Slot {SlotId} not found during waitlist offer cascade.", shiftSlotId);
            return;
        }

        // Load group config for WaitlistOfferMinutes
        var config = await _db.SelfServiceConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.GroupId == slot.GroupId, ct);

        var offerMinutes = config?.WaitlistOfferMinutes ?? 60;

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        var expiresAt = utcNow.AddMinutes(offerMinutes);

        // Offer the slot to this member
        nextEntry.Offer(expiresAt);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Offered slot {SlotId} to person {PersonId} (position {Position}). Expires at {ExpiresAt}.",
            shiftSlotId, nextEntry.PersonId, nextEntry.Position, expiresAt);

        // Req 13.3: Send notification for waitlist offer
        await SendWaitlistOfferNotificationAsync(nextEntry.PersonId, slot, expiresAt, ct);
    }

    /// <summary>
    /// Sends an in-app and push notification when a waitlisted member is offered a slot (Req 13.3).
    /// Includes shift name, date, time, and acceptance deadline.
    /// Push failures are logged but do not affect in-app notification persistence (Req 13.7).
    /// </summary>
    private async Task SendWaitlistOfferNotificationAsync(
        Guid personId, ShiftSlot slot, DateTime expiresAt, CancellationToken ct)
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

            var title = "Waitlist Slot Available";
            var body = $"A spot has opened for {taskName} on {slot.Date:MMM dd} ({slot.StartTime:HH:mm}–{slot.EndTime:HH:mm}). " +
                       $"Accept before {expiresAt:MMM dd, HH:mm} UTC to claim it.";

            var notification = Notification.Create(
                spaceId: slot.SpaceId,
                userId: person.LinkedUserId.Value,
                eventType: "self_service.waitlist_offer",
                title: title,
                body: body,
                metadataJson: JsonSerializer.Serialize(new
                {
                    shiftSlotId = slot.Id,
                    groupId = slot.GroupId,
                    date = slot.Date,
                    startTime = slot.StartTime.ToString("HH:mm"),
                    endTime = slot.EndTime.ToString("HH:mm"),
                    taskName,
                    expiresAt,
                    acceptUrl = $"/shifts/waitlist/accept?slotId={slot.Id}"
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
                    Url: $"/shifts/waitlist/accept?slotId={slot.Id}");

                await _pushSender.SendPushToUserAsync(person.LinkedUserId.Value, slot.SpaceId, payload, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Push notification delivery failed for waitlist offer (person {PersonId}, slot {SlotId}). " +
                    "In-app notification was persisted successfully.",
                    personId, slot.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send waitlist offer notification for person {PersonId}, slot {SlotId}",
                personId, slot.Id);
        }
    }

    private async Task<List<WaitlistExpiryNotification>> BuildWaitlistOfferExpiredNotificationsAsync(
        IReadOnlyCollection<WaitlistEntry> expiredEntries,
        CancellationToken ct)
    {
        if (expiredEntries.Count == 0)
            return [];

        var personIds = expiredEntries.Select(e => e.PersonId).Distinct().ToList();
        var slotIds = expiredEntries.Select(e => e.ShiftSlotId).Distinct().ToList();

        var linkedUsers = await _db.People
            .AsNoTracking()
            .Where(p => personIds.Contains(p.Id) && p.LinkedUserId != null)
            .Select(p => new { p.Id, p.LinkedUserId })
            .ToDictionaryAsync(p => p.Id, p => p.LinkedUserId!.Value, ct);

        var slotDetails = await _db.ShiftSlots
            .AsNoTracking()
            .Where(s => slotIds.Contains(s.Id))
            .Join(
                _db.GroupTasks.AsNoTracking(),
                s => s.GroupTaskId,
                t => t.Id,
                (s, t) => new { Slot = s, TaskName = t.Name })
            .ToDictionaryAsync(s => s.Slot.Id, s => s, ct);

        var notifications = new List<WaitlistExpiryNotification>();

        foreach (var entry in expiredEntries)
        {
            if (!linkedUsers.TryGetValue(entry.PersonId, out var userId)
                || !slotDetails.TryGetValue(entry.ShiftSlotId, out var detail))
            {
                continue;
            }

            var title = "Waitlist Offer Expired";
            var body = $"Your waitlist offer for {detail.TaskName} on {detail.Slot.Date:MMM dd} expired.";
            var metadataJson = JsonSerializer.Serialize(new
            {
                waitlistEntryId = entry.Id,
                shiftSlotId = entry.ShiftSlotId,
                groupId = detail.Slot.GroupId,
                date = detail.Slot.Date,
                startTime = detail.Slot.StartTime.ToString("HH:mm"),
                endTime = detail.Slot.EndTime.ToString("HH:mm"),
                taskName = detail.TaskName,
                offeredAt = entry.OfferedAt,
                expiresAt = entry.ExpiresAt
            });

            notifications.Add(new WaitlistExpiryNotification(
                Notification.Create(
                    detail.Slot.SpaceId,
                    userId,
                    "self_service.waitlist_offer_expired",
                    title,
                    body,
                    metadataJson),
                userId,
                detail.Slot.SpaceId,
                title,
                body));
        }

        return notifications;
    }

    private async Task TrySendWaitlistOfferExpiredPushAsync(WaitlistExpiryNotification notification, CancellationToken ct)
    {
        try
        {
            await _pushSender.SendPushToUserAsync(
                notification.UserId,
                notification.SpaceId,
                new PushPayload(notification.Title, notification.Body, "/favicon.jpeg", "/shifts"),
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Push notification delivery failed for expired waitlist offer (user {UserId}, space {SpaceId}). " +
                "In-app notification was persisted successfully.",
                notification.UserId,
                notification.SpaceId);
        }
    }

    private sealed record WaitlistExpiryNotification(
        Notification Notification,
        Guid UserId,
        Guid SpaceId,
        string Title,
        string Body);
}
