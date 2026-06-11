using Jobuler.Application.Common;
using Jobuler.Application.Notifications;
using Jobuler.Application.Scheduling.SelfService.Models;
using Jobuler.Domain.Conflicts;
using Jobuler.Domain.Notifications;
using Jobuler.Domain.Scheduling;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Jobuler.Application.Scheduling.SelfService;

/// <summary>
/// Manages shift swap proposals between members.
/// Validates ownership, conflict detection via ConflictDetector, and atomic reassignment of shifts.
/// Requirements: 12.1, 12.2, 12.3, 12.4, 12.5, 12.6, 12.8, 12.9
/// </summary>
public class ShiftSwapService : IShiftSwapService
{
    private readonly AppDbContext _db;
    private readonly IPushNotificationSender _pushSender;
    private readonly IAuditLogger _audit;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ShiftSwapService> _logger;

    public ShiftSwapService(
        AppDbContext db,
        IPushNotificationSender pushSender,
        IAuditLogger audit,
        TimeProvider timeProvider,
        ILogger<ShiftSwapService> logger)
    {
        _db = db;
        _pushSender = pushSender;
        _audit = audit;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<SwapResult> ProposeSwapAsync(
        Guid initiatorPersonId,
        Guid initiatorRequestId,
        Guid targetRequestId,
        CancellationToken ct = default)
    {
        // Load both shift requests
        var initiatorRequest = await _db.ShiftRequests
            .FirstOrDefaultAsync(r => r.Id == initiatorRequestId, ct);

        if (initiatorRequest is null)
        {
            return new SwapResult(Success: false, SwapRequestId: null,
                ErrorMessage: "The initiator's shift request was not found.");
        }

        var targetRequest = await _db.ShiftRequests
            .FirstOrDefaultAsync(r => r.Id == targetRequestId, ct);

        if (targetRequest is null)
        {
            return new SwapResult(Success: false, SwapRequestId: null,
                ErrorMessage: "The target's shift request was not found.");
        }

        // Req 12.9: Validate ownership — initiator must own the offered request
        if (initiatorRequest.PersonId != initiatorPersonId)
        {
            return new SwapResult(Success: false, SwapRequestId: null,
                ErrorMessage: "You do not own the offered shift assignment.");
        }

        // Req 12.9: Validate ownership — target must own the requested assignment
        // (target person is the person on the target request)
        if (targetRequest.PersonId == initiatorPersonId)
        {
            return new SwapResult(Success: false, SwapRequestId: null,
                ErrorMessage: "You cannot propose a swap with yourself.");
        }

        // Req 12.1: Both requests must be in Approved status
        if (initiatorRequest.Status != ShiftRequestStatus.Approved)
        {
            return new SwapResult(Success: false, SwapRequestId: null,
                ErrorMessage: "The initiator's shift request must be in Approved status.");
        }

        if (targetRequest.Status != ShiftRequestStatus.Approved)
        {
            return new SwapResult(Success: false, SwapRequestId: null,
                ErrorMessage: "The target's shift request must be in Approved status.");
        }

        // Req 12.1: Both requests must belong to the same group
        if (initiatorRequest.GroupId != targetRequest.GroupId)
        {
            return new SwapResult(Success: false, SwapRequestId: null,
                ErrorMessage: "Both shift requests must belong to the same group.");
        }

        if (initiatorRequest.SchedulingCycleId != targetRequest.SchedulingCycleId)
        {
            return new SwapResult(Success: false, SwapRequestId: null,
                ErrorMessage: "Both shift requests must belong to the same scheduling cycle.");
        }

        // Req 12.1: Both shifts must start in the future
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

        var initiatorSlot = await _db.ShiftSlots
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == initiatorRequest.ShiftSlotId, ct);

        var targetSlot = await _db.ShiftSlots
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == targetRequest.ShiftSlotId, ct);

        if (initiatorSlot is null || targetSlot is null)
        {
            return new SwapResult(Success: false, SwapRequestId: null,
                ErrorMessage: "One or both shift slots could not be found.");
        }

        if (!SlotMatchesRequest(initiatorRequest, initiatorSlot) || !SlotMatchesRequest(targetRequest, targetSlot))
        {
            return new SwapResult(Success: false, SwapRequestId: null,
                ErrorMessage: "Shift request metadata no longer matches its assigned slot.");
        }

        var initiatorShiftStart = initiatorSlot.Date.ToDateTime(initiatorSlot.StartTime, DateTimeKind.Utc);
        var targetShiftStart = targetSlot.Date.ToDateTime(targetSlot.StartTime, DateTimeKind.Utc);

        if (initiatorShiftStart <= utcNow)
        {
            return new SwapResult(Success: false, SwapRequestId: null,
                ErrorMessage: "The initiator's shift has already started or is in the past.");
        }

        if (targetShiftStart <= utcNow)
        {
            return new SwapResult(Success: false, SwapRequestId: null,
                ErrorMessage: "The target's shift has already started or is in the past.");
        }

        // Req 12.8: No pending swap on either shift request
        var hasPendingSwapOnInitiator = await _db.SwapRequests
            .AnyAsync(s => s.Status == SwapRequestStatus.Pending
                           && (s.InitiatorShiftRequestId == initiatorRequestId
                               || s.TargetShiftRequestId == initiatorRequestId), ct);

        if (hasPendingSwapOnInitiator)
        {
            return new SwapResult(Success: false, SwapRequestId: null,
                ErrorMessage: "The initiator's shift already has a pending swap request.");
        }

        var hasPendingSwapOnTarget = await _db.SwapRequests
            .AnyAsync(s => s.Status == SwapRequestStatus.Pending
                           && (s.InitiatorShiftRequestId == targetRequestId
                               || s.TargetShiftRequestId == targetRequestId), ct);

        if (hasPendingSwapOnTarget)
        {
            return new SwapResult(Success: false, SwapRequestId: null,
                ErrorMessage: "The target's shift already has a pending swap request.");
        }

        // All validations passed — create the swap request with 72h expiry
        var swapRequest = SwapRequest.Create(
            spaceId: initiatorRequest.SpaceId,
            groupId: initiatorRequest.GroupId,
            initiatorPersonId: initiatorPersonId,
            targetPersonId: targetRequest.PersonId,
            initiatorShiftRequestId: initiatorRequestId,
            targetShiftRequestId: targetRequestId);

        _db.SwapRequests.Add(swapRequest);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(
            swapRequest.SpaceId,
            await ResolveLinkedUserIdAsync(swapRequest.InitiatorPersonId, swapRequest.SpaceId, ct),
            "self_service.propose_swap",
            "swap_request",
            swapRequest.Id,
            afterJson: JsonSerializer.Serialize(new
            {
                swap_request_id = swapRequest.Id,
                group_id = swapRequest.GroupId,
                initiator_person_id = swapRequest.InitiatorPersonId,
                target_person_id = swapRequest.TargetPersonId,
                initiator_shift_request_id = swapRequest.InitiatorShiftRequestId,
                target_shift_request_id = swapRequest.TargetShiftRequestId,
                initiator_shift_slot_id = initiatorRequest.ShiftSlotId,
                target_shift_slot_id = targetRequest.ShiftSlotId,
                status = swapRequest.Status.ToString().ToLowerInvariant(),
                expires_at = swapRequest.ExpiresAt
            }),
            ct: ct);

        _logger.LogInformation(
            "Swap request {SwapId} created: initiator {InitiatorId} offers request {InitReqId} for target request {TargetReqId}",
            swapRequest.Id, initiatorPersonId, initiatorRequestId, targetRequestId);

        // Req 12.2 / 13.5: Notify target member of the swap proposal
        await SendSwapProposalNotificationAsync(
            initiatorPersonId, targetRequest.PersonId, swapRequest.Id,
            initiatorSlot, targetSlot, initiatorRequest.SpaceId, ct);

        return new SwapResult(Success: true, SwapRequestId: swapRequest.Id, ErrorMessage: null);
    }

    /// <inheritdoc />
    public async Task<SwapResult> AcceptSwapAsync(
        Guid targetPersonId,
        Guid swapRequestId,
        CancellationToken ct = default)
    {
        var strategy = _db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                var swapRequest = await _db.SwapRequests
                    .FirstOrDefaultAsync(s => s.Id == swapRequestId, ct);

                if (swapRequest is null)
                {
                    await transaction.RollbackAsync(ct);
                    return new SwapResult(Success: false, SwapRequestId: null,
                        ErrorMessage: "Swap request not found.");
                }

                // Only the target person can accept
                if (swapRequest.TargetPersonId != targetPersonId)
                {
                    await transaction.RollbackAsync(ct);
                    return new SwapResult(Success: false, SwapRequestId: null,
                        ErrorMessage: "Only the target member can accept this swap request.");
                }

                // Must be in Pending status
                if (swapRequest.Status != SwapRequestStatus.Pending)
                {
                    await transaction.RollbackAsync(ct);
                    return new SwapResult(Success: false, SwapRequestId: null,
                        ErrorMessage: "Only pending swap requests can be accepted.");
                }

                // Check if expired
                var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
                if (swapRequest.ExpiresAt.HasValue && utcNow > swapRequest.ExpiresAt.Value)
                {
                    swapRequest.Expire();
                    await _db.SaveChangesAsync(ct);
                    await transaction.CommitAsync(ct);
                    return new SwapResult(Success: false, SwapRequestId: swapRequestId,
                        ErrorMessage: "This swap request has expired.");
                }

                // Load both shift requests
                var initiatorRequest = await _db.ShiftRequests
                    .FirstOrDefaultAsync(r => r.Id == swapRequest.InitiatorShiftRequestId, ct);

                var targetRequest = await _db.ShiftRequests
                    .FirstOrDefaultAsync(r => r.Id == swapRequest.TargetShiftRequestId, ct);

                if (initiatorRequest is null || targetRequest is null)
                {
                    await transaction.RollbackAsync(ct);
                    return new SwapResult(Success: false, SwapRequestId: swapRequestId,
                        ErrorMessage: "One or both shift requests could not be found.");
                }

                // Both must still be Approved
                if (initiatorRequest.Status != ShiftRequestStatus.Approved ||
                    targetRequest.Status != ShiftRequestStatus.Approved)
                {
                    await transaction.RollbackAsync(ct);
                    return new SwapResult(Success: false, SwapRequestId: swapRequestId,
                        ErrorMessage: "Both shift requests must still be in Approved status to complete the swap.");
                }

                if (initiatorRequest.SchedulingCycleId != targetRequest.SchedulingCycleId)
                {
                    await transaction.RollbackAsync(ct);
                    return new SwapResult(Success: false, SwapRequestId: swapRequestId,
                        ErrorMessage: "Both shift requests must belong to the same scheduling cycle to complete the swap.");
                }

                // Load both slots for conflict detection
                var initiatorSlot = await _db.ShiftSlots
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Id == initiatorRequest.ShiftSlotId, ct);

                var targetSlot = await _db.ShiftSlots
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Id == targetRequest.ShiftSlotId, ct);

                if (initiatorSlot is null || targetSlot is null)
                {
                    await transaction.RollbackAsync(ct);
                    return new SwapResult(Success: false, SwapRequestId: swapRequestId,
                        ErrorMessage: "One or both shift slots could not be found.");
                }

                if (!SlotMatchesRequest(initiatorRequest, initiatorSlot) || !SlotMatchesRequest(targetRequest, targetSlot))
                {
                    await transaction.RollbackAsync(ct);
                    return new SwapResult(Success: false, SwapRequestId: swapRequestId,
                        ErrorMessage: "Shift request metadata no longer matches its assigned slot.");
                }

                var initiatorAssignmentConflict = await ShiftAssignmentSafety.FindApprovedAssignmentConflictAsync(
                    _db,
                    swapRequest.InitiatorPersonId,
                    targetSlot,
                    ct,
                    excludeShiftSlotId: initiatorRequest.ShiftSlotId);

                if (initiatorAssignmentConflict != ShiftAssignmentConflictKind.None)
                {
                    await transaction.RollbackAsync(ct);
                    return new SwapResult(Success: false, SwapRequestId: swapRequestId,
                        ErrorMessage: FormatSwapAssignmentConflict("initiator", initiatorAssignmentConflict));
                }

                var targetAssignmentConflict = await ShiftAssignmentSafety.FindApprovedAssignmentConflictAsync(
                    _db,
                    swapRequest.TargetPersonId,
                    initiatorSlot,
                    ct,
                    excludeShiftSlotId: targetRequest.ShiftSlotId);

                if (targetAssignmentConflict != ShiftAssignmentConflictKind.None)
                {
                    await transaction.RollbackAsync(ct);
                    return new SwapResult(Success: false, SwapRequestId: swapRequestId,
                        ErrorMessage: FormatSwapAssignmentConflict("target", targetAssignmentConflict));
                }

                // Req 12.3, 12.4: Run ConflictDetector on hypothetical post-swap state
                // After swap: initiator gets target's slot, target gets initiator's slot
                var conflictResult = await DetectSwapConflictsAsync(
                    swapRequest.InitiatorPersonId,
                    swapRequest.TargetPersonId,
                    initiatorRequest,
                    targetRequest,
                    initiatorSlot,
                    targetSlot,
                    ct);

                if (conflictResult.Conflicts.Count > 0)
                {
                    var firstConflict = conflictResult.Conflicts[0];
                    var conflictingMember = firstConflict.A.GroupId == initiatorSlot.GroupId
                        ? "initiator"
                        : "target";

                    await transaction.RollbackAsync(ct);
                    return new SwapResult(Success: false, SwapRequestId: swapRequestId,
                        ErrorMessage: $"Conflict detected for the {conflictingMember}: " +
                                      $"{firstConflict.Type} between shifts " +
                                      $"({firstConflict.A.StartsAt:u} - {firstConflict.A.EndsAt:u}) and " +
                                      $"({firstConflict.B.StartsAt:u} - {firstConflict.B.EndsAt:u}).");
                }

                // Req 12.3: Atomically reassign both shifts.
                // Each member keeps their own shift request record; only the assigned slots change.
                var initiatorPersonId = initiatorRequest.PersonId;
                var targetPersonIdOnRequest = targetRequest.PersonId;
                var initiatorSlotId = initiatorRequest.ShiftSlotId;
                var targetSlotId = targetRequest.ShiftSlotId;

                initiatorRequest.ReassignTo(initiatorPersonId, targetSlotId);
                targetRequest.ReassignTo(targetPersonIdOnRequest, initiatorSlotId);

                // Mark swap as accepted
                swapRequest.Accept();

                await _db.SaveChangesAsync(ct);
                await _audit.LogAsync(
                    swapRequest.SpaceId,
                    await ResolveLinkedUserIdAsync(swapRequest.TargetPersonId, swapRequest.SpaceId, ct),
                    "self_service.accept_swap",
                    "swap_request",
                    swapRequest.Id,
                    beforeJson: JsonSerializer.Serialize(new
                    {
                        swap_request_id = swapRequest.Id,
                        group_id = swapRequest.GroupId,
                        initiator_person_id = swapRequest.InitiatorPersonId,
                        target_person_id = swapRequest.TargetPersonId,
                        initiator_shift_request_id = swapRequest.InitiatorShiftRequestId,
                        target_shift_request_id = swapRequest.TargetShiftRequestId,
                        initiator_shift_slot_id = initiatorSlotId,
                        target_shift_slot_id = targetSlotId,
                        status = "pending"
                    }),
                    afterJson: JsonSerializer.Serialize(new
                    {
                        swap_request_id = swapRequest.Id,
                        group_id = swapRequest.GroupId,
                        initiator_person_id = swapRequest.InitiatorPersonId,
                        target_person_id = swapRequest.TargetPersonId,
                        initiator_shift_request_id = swapRequest.InitiatorShiftRequestId,
                        target_shift_request_id = swapRequest.TargetShiftRequestId,
                        initiator_new_shift_slot_id = targetSlotId,
                        target_new_shift_slot_id = initiatorSlotId,
                        status = swapRequest.Status.ToString().ToLowerInvariant()
                    }),
                    ct: ct);
                await transaction.CommitAsync(ct);

                _logger.LogInformation(
                    "Swap request {SwapId} accepted. Initiator {InitiatorId} now on slot {TargetSlotId}, " +
                    "target {TargetId} now on slot {InitiatorSlotId}",
                    swapRequestId, initiatorPersonId, targetSlotId,
                    targetPersonIdOnRequest, initiatorSlotId);

                await SendSwapAcceptedNotificationsAsync(swapRequest, initiatorSlot, targetSlot, ct);

                return new SwapResult(Success: true, SwapRequestId: swapRequestId, ErrorMessage: null);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await transaction.RollbackAsync(ct);
                _logger.LogError(ex, "Error accepting swap request {SwapId}", swapRequestId);
                throw;
            }
        });
    }

    private static bool SlotMatchesRequest(ShiftRequest request, ShiftSlot slot) =>
        request.SpaceId == slot.SpaceId
        && request.GroupId == slot.GroupId
        && request.SchedulingCycleId == slot.SchedulingCycleId;

    private static string FormatSwapAssignmentConflict(string memberRole, ShiftAssignmentConflictKind conflictKind) =>
        conflictKind == ShiftAssignmentConflictKind.Overlap
            ? $"Conflict detected for the {memberRole}: the swapped shift overlaps with an existing approved shift."
            : $"Conflict detected for the {memberRole}: the swapped shift does not leave enough rest time after an existing approved shift.";

    /// <inheritdoc />
    public async Task DeclineSwapAsync(
        Guid targetPersonId,
        Guid swapRequestId,
        CancellationToken ct = default)
    {
        var strategy = _db.Database.CreateExecutionStrategy();

        var swapRequest = await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);

            try
            {
                var swapRequest = await _db.SwapRequests
                    .FirstOrDefaultAsync(s => s.Id == swapRequestId, ct);

                if (swapRequest is null)
                    throw new KeyNotFoundException("Swap request not found.");

                // Only the target person can decline
                if (swapRequest.TargetPersonId != targetPersonId)
                    throw new UnauthorizedAccessException("Only the target member can decline this swap request.");

                if (swapRequest.Status != SwapRequestStatus.Pending)
                    throw new InvalidOperationException("Only pending swap requests can be declined.");

                swapRequest.Decline();
                await _db.SaveChangesAsync(ct);
                await _audit.LogAsync(
                    swapRequest.SpaceId,
                    await ResolveLinkedUserIdAsync(swapRequest.TargetPersonId, swapRequest.SpaceId, ct),
                    "self_service.decline_swap",
                    "swap_request",
                    swapRequest.Id,
                    beforeJson: JsonSerializer.Serialize(new
                    {
                        swap_request_id = swapRequest.Id,
                        group_id = swapRequest.GroupId,
                        initiator_person_id = swapRequest.InitiatorPersonId,
                        target_person_id = swapRequest.TargetPersonId,
                        status = "pending"
                    }),
                    afterJson: JsonSerializer.Serialize(new
                    {
                        swap_request_id = swapRequest.Id,
                        group_id = swapRequest.GroupId,
                        initiator_person_id = swapRequest.InitiatorPersonId,
                        target_person_id = swapRequest.TargetPersonId,
                        status = swapRequest.Status.ToString().ToLowerInvariant()
                    }),
                    ct: ct);

                await transaction.CommitAsync(ct);
                return swapRequest;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await transaction.RollbackAsync(ct);
                _logger.LogError(ex, "Error declining swap request {SwapId}", swapRequestId);
                throw;
            }
        });

        _logger.LogInformation(
            "Swap request {SwapId} declined by target {TargetId}",
            swapRequestId, targetPersonId);

        // Req 12.5: Notify initiator of the decline
        await SendSwapDeclinedNotificationAsync(swapRequest, ct);
    }

    /// <inheritdoc />
    public async Task CancelSwapAsync(
        Guid initiatorPersonId,
        Guid swapRequestId,
        CancellationToken ct = default)
    {
        var strategy = _db.Database.CreateExecutionStrategy();

        var swapRequest = await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);

            try
            {
                var swapRequest = await _db.SwapRequests
                    .FirstOrDefaultAsync(s => s.Id == swapRequestId, ct);

                if (swapRequest is null)
                    throw new KeyNotFoundException("Swap request not found.");

                // Only the initiator can cancel
                if (swapRequest.InitiatorPersonId != initiatorPersonId)
                    throw new UnauthorizedAccessException("Only the initiator can cancel this swap request.");

                // Req 12.6: Can only cancel if still Pending
                if (swapRequest.Status != SwapRequestStatus.Pending)
                    throw new InvalidOperationException("Only pending swap requests can be cancelled.");

                swapRequest.Cancel();
                await _db.SaveChangesAsync(ct);
                await _audit.LogAsync(
                    swapRequest.SpaceId,
                    await ResolveLinkedUserIdAsync(swapRequest.InitiatorPersonId, swapRequest.SpaceId, ct),
                    "self_service.cancel_swap",
                    "swap_request",
                    swapRequest.Id,
                    beforeJson: JsonSerializer.Serialize(new
                    {
                        swap_request_id = swapRequest.Id,
                        group_id = swapRequest.GroupId,
                        initiator_person_id = swapRequest.InitiatorPersonId,
                        target_person_id = swapRequest.TargetPersonId,
                        status = "pending"
                    }),
                    afterJson: JsonSerializer.Serialize(new
                    {
                        swap_request_id = swapRequest.Id,
                        group_id = swapRequest.GroupId,
                        initiator_person_id = swapRequest.InitiatorPersonId,
                        target_person_id = swapRequest.TargetPersonId,
                        status = swapRequest.Status.ToString().ToLowerInvariant()
                    }),
                    ct: ct);

                await transaction.CommitAsync(ct);
                return swapRequest;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await transaction.RollbackAsync(ct);
                _logger.LogError(ex, "Error cancelling swap request {SwapId}", swapRequestId);
                throw;
            }
        });

        _logger.LogInformation(
            "Swap request {SwapId} cancelled by initiator {InitiatorId}",
            swapRequestId, initiatorPersonId);

        await SendSwapCancelledNotificationAsync(swapRequest, ct);
    }

    private async Task<Guid?> ResolveLinkedUserIdAsync(Guid personId, Guid spaceId, CancellationToken ct) =>
        await _db.People
            .AsNoTracking()
            .Where(p => p.Id == personId && p.SpaceId == spaceId)
            .Select(p => p.LinkedUserId)
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// Detects conflicts in the hypothetical post-swap state for both members.
    /// Projects the post-swap assignments into FlatAssignment records and runs ConflictDetector.
    /// </summary>
    private async Task<ConflictResult> DetectSwapConflictsAsync(
        Guid initiatorPersonId,
        Guid targetPersonId,
        ShiftRequest initiatorRequest,
        ShiftRequest targetRequest,
        ShiftSlot initiatorSlot,
        ShiftSlot targetSlot,
        CancellationToken ct)
    {
        // Load the group to get MinRestBetweenShiftsHours
        var group = await _db.Groups
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == initiatorRequest.GroupId, ct);

        var minRestHours = group?.MinRestBetweenShiftsHours ?? 8;

        // Check conflicts for the initiator (who will get the target's slot)
        var initiatorConflicts = await CheckPersonConflictsAsync(
            initiatorPersonId,
            initiatorRequest.ShiftSlotId, // exclude their current slot (being swapped away)
            targetSlot, // they're getting this slot
            initiatorRequest.GroupId,
            minRestHours,
            ct);

        if (initiatorConflicts.Conflicts.Count > 0)
            return initiatorConflicts;

        // Check conflicts for the target (who will get the initiator's slot)
        var targetConflicts = await CheckPersonConflictsAsync(
            targetPersonId,
            targetRequest.ShiftSlotId, // exclude their current slot (being swapped away)
            initiatorSlot, // they're getting this slot
            targetRequest.GroupId,
            minRestHours,
            ct);

        return targetConflicts;
    }

    /// <summary>
    /// Checks if a person would have conflicts after receiving a new slot
    /// (with their old slot removed from their assignments).
    /// </summary>
    private async Task<ConflictResult> CheckPersonConflictsAsync(
        Guid personId,
        Guid excludeSlotId,
        ShiftSlot newSlot,
        Guid groupId,
        int minRestHours,
        CancellationToken ct)
    {
        // Load all approved shift requests for this person (across all groups for cross-group detection)
        var personApprovedRequests = await _db.ShiftRequests
            .AsNoTracking()
            .Where(r => r.PersonId == personId
                        && r.Status == ShiftRequestStatus.Approved
                        && r.ShiftSlotId != excludeSlotId) // Exclude the slot being swapped away
            .ToListAsync(ct);

        // Load the corresponding slots
        var slotIds = personApprovedRequests.Select(r => r.ShiftSlotId).Distinct().ToList();
        var slots = await _db.ShiftSlots
            .AsNoTracking()
            .Where(s => slotIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, ct);

        // Build FlatAssignment list for the person's current state (minus swapped slot, plus new slot)
        var assignments = new List<FlatAssignment>();

        foreach (var req in personApprovedRequests)
        {
            if (!slots.TryGetValue(req.ShiftSlotId, out var slot))
                continue;

            var startsAt = slot.Date.ToDateTime(slot.StartTime, DateTimeKind.Utc);
            var endsAt = slot.Date.ToDateTime(slot.EndTime, DateTimeKind.Utc);

            assignments.Add(new FlatAssignment(
                AssignmentId: req.Id,
                GroupId: req.GroupId,
                GroupName: string.Empty, // Not needed for conflict detection
                TaskSlotId: slot.Id,
                StartsAt: startsAt,
                EndsAt: endsAt));
        }

        // Add the new slot (the one they're receiving from the swap)
        var newStartsAt = newSlot.Date.ToDateTime(newSlot.StartTime, DateTimeKind.Utc);
        var newEndsAt = newSlot.Date.ToDateTime(newSlot.EndTime, DateTimeKind.Utc);

        assignments.Add(new FlatAssignment(
            AssignmentId: Guid.NewGuid(), // Placeholder ID for the hypothetical assignment
            GroupId: groupId,
            GroupName: string.Empty,
            TaskSlotId: newSlot.Id,
            StartsAt: newStartsAt,
            EndsAt: newEndsAt));

        // Run ConflictDetector
        // The getMinRestHours function returns the max of both groups' MinRestBetweenShiftsHours
        // For same-group swaps, ConflictDetector skips same-group pairs, so we only detect cross-group conflicts
        var result = ConflictDetector.Detect(
            assignments,
            (groupA, groupB) => minRestHours); // Use the group's configured rest hours

        return result;
    }

    /// <summary>
    /// Sends an in-app and push notification when a swap proposal is received (Req 13.5).
    /// Includes the proposing member's name, the shift being offered, and the shift being requested.
    /// Push failures are logged but do not affect in-app notification persistence (Req 13.7).
    /// </summary>
    private async Task SendSwapProposalNotificationAsync(
        Guid initiatorPersonId, Guid targetPersonId, Guid swapRequestId,
        ShiftSlot initiatorSlot, ShiftSlot targetSlot, Guid spaceId,
        CancellationToken ct)
    {
        try
        {
            var persons = await _db.People
                .AsNoTracking()
                .Where(p => (p.Id == initiatorPersonId || p.Id == targetPersonId) && p.SpaceId == spaceId)
                .Select(p => new { p.Id, p.FullName, p.LinkedUserId })
                .ToListAsync(ct);

            var initiator = persons.FirstOrDefault(p => p.Id == initiatorPersonId);
            var target = persons.FirstOrDefault(p => p.Id == targetPersonId);

            if (target?.LinkedUserId is null)
                return;

            var initiatorName = initiator?.FullName ?? "A team member";

            var initiatorTaskName = await _db.GroupTasks
                .AsNoTracking()
                .Where(t => t.Id == initiatorSlot.GroupTaskId)
                .Select(t => t.Name)
                .FirstOrDefaultAsync(ct) ?? "Shift";

            var targetTaskName = await _db.GroupTasks
                .AsNoTracking()
                .Where(t => t.Id == targetSlot.GroupTaskId)
                .Select(t => t.Name)
                .FirstOrDefaultAsync(ct) ?? "Shift";

            var title = "Swap Proposal Received";
            var body = $"{initiatorName} wants to swap their {initiatorTaskName} on {initiatorSlot.Date:MMM dd} " +
                       $"({initiatorSlot.StartTime:HH:mm}–{initiatorSlot.EndTime:HH:mm}) " +
                       $"for your {targetTaskName} on {targetSlot.Date:MMM dd} " +
                       $"({targetSlot.StartTime:HH:mm}–{targetSlot.EndTime:HH:mm}).";

            var notification = Notification.Create(
                spaceId: spaceId,
                userId: target.LinkedUserId.Value,
                eventType: "self_service.swap_proposal_received",
                title: title,
                body: body,
                metadataJson: JsonSerializer.Serialize(new
                {
                    swapRequestId,
                    initiatorPersonId,
                    initiatorName,
                    offeredShift = new
                    {
                        slotId = initiatorSlot.Id,
                        date = initiatorSlot.Date,
                        startTime = initiatorSlot.StartTime.ToString("HH:mm"),
                        endTime = initiatorSlot.EndTime.ToString("HH:mm"),
                        taskName = initiatorTaskName
                    },
                    requestedShift = new
                    {
                        slotId = targetSlot.Id,
                        date = targetSlot.Date,
                        startTime = targetSlot.StartTime.ToString("HH:mm"),
                        endTime = targetSlot.EndTime.ToString("HH:mm"),
                        taskName = targetTaskName
                    }
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
                    Url: $"/shifts/swaps/{swapRequestId}");

                await _pushSender.SendPushToUserAsync(target.LinkedUserId.Value, spaceId, payload, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Push notification delivery failed for swap proposal (target {TargetPersonId}, swap {SwapId}). " +
                    "In-app notification was persisted successfully.",
                    targetPersonId, swapRequestId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send swap proposal notification for swap {SwapId}",
                swapRequestId);
        }
    }

    /// <summary>
    /// Sends an in-app and push notification when a swap is declined (Req 12.5).
    /// Notifies the initiator that their swap proposal was declined.
    /// Push failures are logged but do not affect in-app notification persistence (Req 13.7).
    /// </summary>
    private async Task SendSwapDeclinedNotificationAsync(SwapRequest swapRequest, CancellationToken ct)
    {
        try
        {
            var initiator = await _db.People
                .AsNoTracking()
                .Where(p => p.Id == swapRequest.InitiatorPersonId && p.SpaceId == swapRequest.SpaceId)
                .Select(p => new { p.LinkedUserId })
                .FirstOrDefaultAsync(ct);

            if (initiator?.LinkedUserId is null)
                return;

            var title = "Swap Proposal Declined";
            var body = "Your shift swap proposal has been declined by the other member.";

            var notification = Notification.Create(
                spaceId: swapRequest.SpaceId,
                userId: initiator.LinkedUserId.Value,
                eventType: "self_service.swap_declined",
                title: title,
                body: body,
                metadataJson: JsonSerializer.Serialize(new
                {
                    swapRequestId = swapRequest.Id,
                    groupId = swapRequest.GroupId
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
                    Url: "/shifts/swaps");

                await _pushSender.SendPushToUserAsync(initiator.LinkedUserId.Value, swapRequest.SpaceId, payload, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Push notification delivery failed for swap declined (initiator {InitiatorPersonId}, swap {SwapId}). " +
                    "In-app notification was persisted successfully.",
                    swapRequest.InitiatorPersonId, swapRequest.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send swap declined notification for swap {SwapId}",
                swapRequest.Id);
        }
    }

    private async Task SendSwapAcceptedNotificationsAsync(
        SwapRequest swapRequest,
        ShiftSlot initiatorSlot,
        ShiftSlot targetSlot,
        CancellationToken ct)
    {
        try
        {
            var persons = await _db.People
                .AsNoTracking()
                .Where(p => (p.Id == swapRequest.InitiatorPersonId || p.Id == swapRequest.TargetPersonId)
                            && p.SpaceId == swapRequest.SpaceId
                            && p.LinkedUserId != null)
                .Select(p => new { p.Id, p.LinkedUserId })
                .ToListAsync(ct);

            if (persons.Count == 0)
                return;

            var taskIds = new[] { initiatorSlot.GroupTaskId, targetSlot.GroupTaskId };
            var taskNames = await _db.GroupTasks
                .AsNoTracking()
                .Where(t => taskIds.Contains(t.Id))
                .Select(t => new { t.Id, t.Name })
                .ToDictionaryAsync(t => t.Id, t => t.Name, ct);

            var initiatorTaskName = taskNames.GetValueOrDefault(initiatorSlot.GroupTaskId, "Shift");
            var targetTaskName = taskNames.GetValueOrDefault(targetSlot.GroupTaskId, "Shift");

            var notifications = new List<Notification>();
            foreach (var person in persons)
            {
                var receivingSlot = person.Id == swapRequest.InitiatorPersonId ? targetSlot : initiatorSlot;
                var receivingTask = person.Id == swapRequest.InitiatorPersonId ? targetTaskName : initiatorTaskName;

                notifications.Add(Notification.Create(
                    swapRequest.SpaceId,
                    person.LinkedUserId!.Value,
                    "self_service.swap_accepted",
                    "Shift Swap Accepted",
                    $"Your shift swap was accepted. You are now assigned to {receivingTask} on {receivingSlot.Date:MMM dd} ({receivingSlot.StartTime:HH:mm}-{receivingSlot.EndTime:HH:mm}).",
                    JsonSerializer.Serialize(new
                    {
                        swapRequestId = swapRequest.Id,
                        groupId = swapRequest.GroupId,
                        shiftSlotId = receivingSlot.Id,
                        date = receivingSlot.Date,
                        startTime = receivingSlot.StartTime.ToString("HH:mm"),
                        endTime = receivingSlot.EndTime.ToString("HH:mm"),
                        taskName = receivingTask
                    })));
            }

            _db.Notifications.AddRange(notifications);
            await _db.SaveChangesAsync(ct);

            try
            {
                await _pushSender.SendPushToUsersAsync(
                    persons.Select(p => p.LinkedUserId!.Value).ToList(),
                    swapRequest.SpaceId,
                    new PushPayload(
                        Title: "Shift Swap Accepted",
                        Body: "Your shift swap was accepted.",
                        Icon: "/favicon.jpeg",
                        Url: "/shifts/swaps"),
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Push notification delivery failed for swap accepted (swap {SwapId}). In-app notifications were persisted successfully.",
                    swapRequest.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send swap accepted notifications for swap {SwapId}",
                swapRequest.Id);
        }
    }

    private async Task SendSwapCancelledNotificationAsync(SwapRequest swapRequest, CancellationToken ct)
    {
        try
        {
            var target = await _db.People
                .AsNoTracking()
                .Where(p => p.Id == swapRequest.TargetPersonId
                            && p.SpaceId == swapRequest.SpaceId
                            && p.LinkedUserId != null)
                .Select(p => new { p.LinkedUserId })
                .FirstOrDefaultAsync(ct);

            if (target?.LinkedUserId is null)
                return;

            var title = "Swap Proposal Cancelled";
            var body = "A shift swap proposal sent to you was cancelled.";

            _db.Notifications.Add(Notification.Create(
                swapRequest.SpaceId,
                target.LinkedUserId.Value,
                "self_service.swap_cancelled",
                title,
                body,
                JsonSerializer.Serialize(new
                {
                    swapRequestId = swapRequest.Id,
                    groupId = swapRequest.GroupId
                })));

            await _db.SaveChangesAsync(ct);

            try
            {
                await _pushSender.SendPushToUserAsync(
                    target.LinkedUserId.Value,
                    swapRequest.SpaceId,
                    new PushPayload(title, body, "/favicon.jpeg", "/shifts/swaps"),
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Push notification delivery failed for swap cancelled (swap {SwapId}). In-app notification was persisted successfully.",
                    swapRequest.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send swap cancelled notification for swap {SwapId}",
                swapRequest.Id);
        }
    }
}
