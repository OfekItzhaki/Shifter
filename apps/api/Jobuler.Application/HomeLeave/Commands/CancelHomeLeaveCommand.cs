using System.Text.Json;
using Jobuler.Application.Common;
using Jobuler.Application.HomeLeave.Services;
using Jobuler.Domain.People;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.HomeLeave.Commands;

public record CancelHomeLeaveCommand(
    Guid SpaceId,
    Guid PersonId,
    Guid PresenceWindowId,
    Guid RequestingUserId,
    bool Confirmed,
    string? Reason = null,
    DateTime? ExpectedReturnAt = null) : IRequest<CancelHomeLeaveResult>;

public record CancelHomeLeaveResult(
    bool Deleted,
    bool Truncated,
    DateTime? TruncatedAt,
    bool NotificationSent);

public class CancelHomeLeaveCommandHandler : IRequestHandler<CancelHomeLeaveCommand, CancelHomeLeaveResult>
{
    private readonly AppDbContext _db;
    private readonly IPermissionService _permissions;
    private readonly IAuditLogger _audit;
    private readonly IRecallNotificationService _recallNotification;

    public CancelHomeLeaveCommandHandler(
        AppDbContext db,
        IPermissionService permissions,
        IAuditLogger audit,
        IRecallNotificationService recallNotification)
    {
        _db = db;
        _permissions = permissions;
        _audit = audit;
        _recallNotification = recallNotification;
    }

    public async Task<CancelHomeLeaveResult> Handle(CancelHomeLeaveCommand req, CancellationToken ct)
    {
        // Block automated invocations — only explicit admin action is allowed.
        // Automated services use Guid.Empty as the requesting user ID.
        if (req.RequestingUserId == Guid.Empty)
            throw new UnauthorizedAccessException("Home leave cancellation requires explicit admin invocation. Automated processes are not permitted.");

        // Reject if not explicitly confirmed (defense-in-depth; validator also checks this).
        if (!req.Confirmed)
            throw new InvalidOperationException("Recall must be explicitly confirmed.");

        // Set PostgreSQL session variables for RLS policies.
        if (_db.Database.IsRelational())
        {
            await _db.Database.ExecuteSqlRawAsync(
                "SELECT set_config('app.current_space_id', {0}, TRUE), set_config('app.current_user_id', {1}, TRUE)",
                req.SpaceId.ToString(),
                req.RequestingUserId.ToString());
        }

        // Verify the requesting user has SchedulePublish permission.
        await _permissions.RequirePermissionAsync(req.RequestingUserId, req.SpaceId, Permissions.SchedulePublish, ct);

        // When EmergencyFreeze is NOT active, require explicit override confirmation
        // for cancelling active home leave. Load the person's group config to check.
        var groupId = await _db.GroupMemberships.AsNoTracking()
            .Where(m => m.PersonId == req.PersonId && m.SpaceId == req.SpaceId)
            .Select(m => m.GroupId)
            .FirstOrDefaultAsync(ct);

        if (groupId != Guid.Empty)
        {
            var hlConfig = await _db.HomeLeaveConfigs.AsNoTracking()
                .FirstOrDefaultAsync(c => c.GroupId == groupId && c.SpaceId == req.SpaceId, ct);

            // When EmergencyFreeze is NOT active, the Confirmed flag is mandatory
            // (already enforced by validator and the check above). This block ensures
            // that even if the validator is bypassed, the handler rejects unconfirmed recalls
            // outside of emergency mode.
            if (hlConfig is not null && !hlConfig.EmergencyFreezeActive && !req.Confirmed)
                throw new InvalidOperationException("Recall of home leave outside emergency freeze requires explicit confirmation.");
        }

        // Load the presence window — must be AtHome, belongs to space and person
        var window = await _db.PresenceWindows
            .FirstOrDefaultAsync(pw =>
                pw.Id == req.PresenceWindowId
                && pw.SpaceId == req.SpaceId
                && pw.PersonId == req.PersonId
                && pw.State == PresenceState.AtHome, ct)
            ?? throw new KeyNotFoundException("Home-leave presence window not found.");

        var now = DateTime.UtcNow;

        // Capture before-snapshot for audit log
        var originalStartsAt = window.StartsAt;
        var originalEndsAt = window.EndsAt;

        if (window.StartsAt > now)
        {
            // Future window: delete entirely
            _db.PresenceWindows.Remove(window);
            await _db.SaveChangesAsync(ct);

            await LogRecallAuditAsync(req, originalStartsAt, originalEndsAt, "deleted", truncatedAt: null, ct);

            var notificationSent = await SendRecallNotificationSafeAsync(req, ct);

            return new CancelHomeLeaveResult(Deleted: true, Truncated: false, TruncatedAt: null, NotificationSent: notificationSent);
        }
        else if (window.EndsAt > now)
        {
            // In-progress window (starts_at in past, ends_at in future): truncate to now
            window.Truncate(now);
            await _db.SaveChangesAsync(ct);

            await LogRecallAuditAsync(req, originalStartsAt, originalEndsAt, "truncated", truncatedAt: now, ct);

            var notificationSent = await SendRecallNotificationSafeAsync(req, ct);

            return new CancelHomeLeaveResult(Deleted: false, Truncated: true, TruncatedAt: now, NotificationSent: notificationSent);
        }
        else
        {
            // Window is entirely in the past — nothing to cancel
            throw new InvalidOperationException("Cannot cancel a home-leave window that has already ended.");
        }
    }

    private async Task LogRecallAuditAsync(
        CancelHomeLeaveCommand req,
        DateTime originalStartsAt,
        DateTime originalEndsAt,
        string operation,
        DateTime? truncatedAt,
        CancellationToken ct)
    {
        var beforeJson = JsonSerializer.Serialize(new
        {
            person_id = req.PersonId,
            starts_at = originalStartsAt,
            ends_at = originalEndsAt,
            operation
        });

        var afterJson = JsonSerializer.Serialize(new
        {
            reason = req.Reason,
            expected_return_at = req.ExpectedReturnAt,
            truncated_at = truncatedAt
        });

        await _audit.LogAsync(
            req.SpaceId,
            req.RequestingUserId,
            "cancel_home_leave",
            entityType: "presence_window",
            entityId: req.PresenceWindowId,
            beforeJson: beforeJson,
            afterJson: afterJson,
            ct: ct);
    }

    private async Task<bool> SendRecallNotificationSafeAsync(CancelHomeLeaveCommand req, CancellationToken ct)
    {
        // Resolve admin display name from the requesting user ID
        var adminName = await _db.Users.AsNoTracking()
            .Where(u => u.Id == req.RequestingUserId)
            .Select(u => u.DisplayName)
            .FirstOrDefaultAsync(ct) ?? "Admin";

        return await _recallNotification.SendRecallNotificationAsync(
            req.SpaceId,
            req.PersonId,
            adminName,
            req.Reason,
            req.ExpectedReturnAt,
            ct);
    }
}
