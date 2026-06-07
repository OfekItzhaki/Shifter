using System.Text.Json;
using Jobuler.Application.Common;
using Jobuler.Application.Scheduling;
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

    public SubmitSpecialLeaveRequestCommandHandler(AppDbContext db) => _db = db;

    public async Task<Guid> Handle(SubmitSpecialLeaveRequestCommand req, CancellationToken ct)
    {
        var personExists = await _db.People.AsNoTracking()
            .AnyAsync(p => p.Id == req.PersonId
                && p.SpaceId == req.SpaceId
                && p.LinkedUserId == req.RequestedByUserId
                && p.IsActive, ct);

        if (!personExists)
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
