using System.Text.Json;
using Jobuler.Application.Common;
using Jobuler.Application.Notifications;
using Jobuler.Application.Scheduling;
using Jobuler.Domain.Notifications;
using Jobuler.Domain.People;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.People.SpecialLeave;

public record SubmitSpecialLeaveRequestCommand(
    Guid SpaceId,
    Guid PersonId,
    DateTime StartsAt,
    DateTime EndsAt,
    string Reason,
    Guid RequestedByUserId) : IRequest<Guid>;

public class SubmitSpecialLeaveRequestCommandHandler
    : IRequestHandler<SubmitSpecialLeaveRequestCommand, Guid>
{
    private readonly AppDbContext _db;
    private readonly INotificationService _notifications;

    public SubmitSpecialLeaveRequestCommandHandler(AppDbContext db, INotificationService notifications)
    {
        _db = db;
        _notifications = notifications;
    }

    public async Task<Guid> Handle(SubmitSpecialLeaveRequestCommand req, CancellationToken ct)
    {
        var person = await _db.People.AsNoTracking()
            .Where(p => p.Id == req.PersonId
                && p.SpaceId == req.SpaceId
                && p.LinkedUserId == req.RequestedByUserId
                && p.IsActive)
            .Select(p => new { p.Id, Name = p.DisplayName ?? p.FullName })
            .FirstOrDefaultAsync(ct);

        if (person is null)
            throw new UnauthorizedAccessException("Current user is not linked to this person in the space.");

        var overlapsExistingRequest = await _db.SpecialLeaveRequests.AsNoTracking()
            .AnyAsync(r => r.SpaceId == req.SpaceId
                && r.PersonId == req.PersonId
                && (r.Status == SpecialLeaveRequestStatus.Pending || r.Status == SpecialLeaveRequestStatus.Approved)
                && r.StartsAt < req.EndsAt
                && r.EndsAt > req.StartsAt, ct);

        if (overlapsExistingRequest)
            throw new InvalidOperationException("An active special leave request already overlaps this time.");

        var request = SpecialLeaveRequest.Create(
            req.SpaceId, req.PersonId, req.StartsAt, req.EndsAt, req.Reason, req.RequestedByUserId);

        _db.SpecialLeaveRequests.Add(request);
        await _db.SaveChangesAsync(ct);
        await _notifications.NotifySpaceAdminsAsync(
            req.SpaceId,
            "self_service.special_leave_requested",
            "Time-off Requested",
            $"{person.Name} requested time off from {req.StartsAt:MMM dd HH:mm} to {req.EndsAt:MMM dd HH:mm}.",
            JsonSerializer.Serialize(new
            {
                requestId = request.Id,
                personId = req.PersonId,
                personName = person.Name,
                startsAt = req.StartsAt,
                endsAt = req.EndsAt,
                reason = req.Reason
            }),
            groupId: null,
            ct);

        return request.Id;
    }
}

public record ApproveSpecialLeaveRequestCommand(
    Guid SpaceId,
    Guid RequestId,
    Guid ProcessedByUserId,
    string? AdminNote,
    Guid? ReasonId = null) : IRequest<Guid>;

public class ApproveSpecialLeaveRequestCommandHandler
    : IRequestHandler<ApproveSpecialLeaveRequestCommand, Guid>
{
    private readonly AppDbContext _db;
    private readonly ICumulativeTracker _cumulativeTracker;
    private readonly ICacheService _cache;
    private readonly IAuditLogger _audit;

    public ApproveSpecialLeaveRequestCommandHandler(
        AppDbContext db,
        ICumulativeTracker cumulativeTracker,
        ICacheService cache,
        IAuditLogger audit)
    {
        _db = db;
        _cumulativeTracker = cumulativeTracker;
        _cache = cache;
        _audit = audit;
    }

    public async Task<Guid> Handle(ApproveSpecialLeaveRequestCommand req, CancellationToken ct)
    {
        var request = await _db.SpecialLeaveRequests
            .FirstOrDefaultAsync(r => r.Id == req.RequestId && r.SpaceId == req.SpaceId, ct)
            ?? throw new KeyNotFoundException("Special leave request not found.");

        var presence = PresenceWindow.CreateManual(
            req.SpaceId,
            request.PersonId,
            PresenceState.AtHome,
            request.StartsAt,
            request.EndsAt,
            BuildPresenceNote(request, req.AdminNote),
            req.ReasonId);

        _db.PresenceWindows.Add(presence);
        request.Approve(req.ProcessedByUserId, presence.Id, req.AdminNote);
        await _db.SaveChangesAsync(ct);

        await _cumulativeTracker.RecomputeForPersonAsync(req.SpaceId, request.PersonId, ct);
        await _cache.RemoveByPatternAsync($"status:{req.SpaceId}:*", ct);

        await _audit.LogAsync(
            req.SpaceId,
            req.ProcessedByUserId,
            "approve_special_leave_request",
            "special_leave_request",
            request.Id,
            afterJson: JsonSerializer.Serialize(new
            {
                request_id = request.Id,
                person_id = request.PersonId,
                starts_at = request.StartsAt,
                ends_at = request.EndsAt,
                presence_window_id = presence.Id
            }),
            ct: ct);

        await SpecialLeaveNotifications.AddMemberReviewNotificationAsync(_db, request, approved: true, ct);

        return presence.Id;
    }

    private static string BuildPresenceNote(SpecialLeaveRequest request, string? adminNote)
    {
        var note = $"Approved special leave: {request.Reason}";
        if (!string.IsNullOrWhiteSpace(adminNote))
            note += $" | Admin note: {adminNote.Trim()}";
        return note.Length <= 500 ? note : note[..500];
    }
}

public record RejectSpecialLeaveRequestCommand(
    Guid SpaceId,
    Guid RequestId,
    Guid ProcessedByUserId,
    string? AdminNote) : IRequest;

public class RejectSpecialLeaveRequestCommandHandler
    : IRequestHandler<RejectSpecialLeaveRequestCommand>
{
    private readonly AppDbContext _db;
    private readonly IAuditLogger _audit;

    public RejectSpecialLeaveRequestCommandHandler(AppDbContext db, IAuditLogger audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task Handle(RejectSpecialLeaveRequestCommand req, CancellationToken ct)
    {
        var request = await _db.SpecialLeaveRequests
            .FirstOrDefaultAsync(r => r.Id == req.RequestId && r.SpaceId == req.SpaceId, ct)
            ?? throw new KeyNotFoundException("Special leave request not found.");

        request.Reject(req.ProcessedByUserId, req.AdminNote);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(
            req.SpaceId,
            req.ProcessedByUserId,
            "reject_special_leave_request",
            "special_leave_request",
            request.Id,
            afterJson: JsonSerializer.Serialize(new
            {
                request_id = request.Id,
                person_id = request.PersonId,
                admin_note = req.AdminNote
            }),
            ct: ct);

        await SpecialLeaveNotifications.AddMemberReviewNotificationAsync(_db, request, approved: false, ct);
    }
}

internal static class SpecialLeaveNotifications
{
    public static async Task AddMemberReviewNotificationAsync(
        AppDbContext db,
        SpecialLeaveRequest request,
        bool approved,
        CancellationToken ct)
    {
        var linkedUserId = await db.People
            .AsNoTracking()
            .Where(p => p.Id == request.PersonId && p.SpaceId == request.SpaceId && p.LinkedUserId != null)
            .Select(p => p.LinkedUserId)
            .FirstOrDefaultAsync(ct);

        if (linkedUserId is null)
            return;

        var title = approved ? "Time-off Approved" : "Time-off Rejected";
        var body = approved
            ? $"Your time-off request from {request.StartsAt:MMM dd HH:mm} to {request.EndsAt:MMM dd HH:mm} was approved."
            : $"Your time-off request from {request.StartsAt:MMM dd HH:mm} to {request.EndsAt:MMM dd HH:mm} was rejected.";

        db.Notifications.Add(Notification.Create(
            request.SpaceId,
            linkedUserId.Value,
            approved ? "self_service.special_leave_approved" : "self_service.special_leave_rejected",
            title,
            body,
            JsonSerializer.Serialize(new
            {
                requestId = request.Id,
                personId = request.PersonId,
                startsAt = request.StartsAt,
                endsAt = request.EndsAt,
                reason = request.Reason,
                adminNote = request.AdminNote,
                processedAt = request.ProcessedAt
            })));

        await db.SaveChangesAsync(ct);
    }
}

public record CancelSpecialLeaveRequestCommand(
    Guid SpaceId,
    Guid RequestId,
    Guid PersonId) : IRequest;

public class CancelSpecialLeaveRequestCommandHandler
    : IRequestHandler<CancelSpecialLeaveRequestCommand>
{
    private readonly AppDbContext _db;

    public CancelSpecialLeaveRequestCommandHandler(AppDbContext db) => _db = db;

    public async Task Handle(CancelSpecialLeaveRequestCommand req, CancellationToken ct)
    {
        var request = await _db.SpecialLeaveRequests
            .FirstOrDefaultAsync(r => r.Id == req.RequestId
                && r.SpaceId == req.SpaceId
                && r.PersonId == req.PersonId, ct)
            ?? throw new KeyNotFoundException("Special leave request not found.");

        request.Cancel();
        await _db.SaveChangesAsync(ct);
    }
}
