using Jobuler.Application.Common;
using Jobuler.Domain.Notifications;
using Jobuler.Domain.Scheduling;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jobuler.Application.Scheduling.Commands;

public record PublishVersionCommand(
    Guid SpaceId,
    Guid VersionId,
    Guid RequestingUserId) : IRequest;

public class PublishVersionCommandHandler : IRequestHandler<PublishVersionCommand>
{
    private readonly AppDbContext _db;
    private readonly IAuditLogger _audit;
    private readonly IScheduleNotificationSender? _scheduleNotifications;
    private readonly IConfiguration _config;
    private readonly ILogger<PublishVersionCommandHandler> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public PublishVersionCommandHandler(
        AppDbContext db,
        IAuditLogger audit,
        IConfiguration config,
        ILogger<PublishVersionCommandHandler> logger,
        IServiceScopeFactory scopeFactory,
        IScheduleNotificationSender? scheduleNotifications = null)
    {
        _db = db;
        _audit = audit;
        _config = config;
        _logger = logger;
        _scopeFactory = scopeFactory;
        _scheduleNotifications = scheduleNotifications;
    }

    public async Task Handle(PublishVersionCommand req, CancellationToken ct)
    {
        // Set RLS session variables so queries on tenant-scoped tables work correctly.
        // Skipped when using an in-memory provider (e.g. unit tests).
        if (_db.Database.IsRelational())
        {
            await _db.Database.ExecuteSqlRawAsync(
                "SELECT set_config('app.current_space_id', {0}, TRUE), set_config('app.current_user_id', {1}, TRUE)",
                req.SpaceId.ToString(), req.RequestingUserId.ToString());
        }

        var version = await _db.ScheduleVersions
            .FirstOrDefaultAsync(v => v.Id == req.VersionId && v.SpaceId == req.SpaceId, ct)
            ?? throw new KeyNotFoundException("Schedule version not found.");

        // Guard: already published — idempotent, just return
        if (version.Status == ScheduleVersionStatus.Published)
            return;

        // Guard: not a draft — cannot publish
        if (version.Status != ScheduleVersionStatus.Draft)
            throw new InvalidOperationException($"Cannot publish a version with status '{version.Status}'.");

        // Archive the current published version before publishing the new one
        var currentPublished = await _db.ScheduleVersions
            .Where(v => v.SpaceId == req.SpaceId && v.Status == ScheduleVersionStatus.Published)
            .ToListAsync(ct);

        foreach (var old in currentPublished)
            old.Archive();

        // Publish enforces draft-only rule inside the domain entity
        version.Publish(req.RequestingUserId);
        await _db.SaveChangesAsync(ct);

        // Send in-app notifications to all space members
        var memberUserIds = await _db.SpaceMemberships.AsNoTracking()
            .Where(m => m.SpaceId == req.SpaceId)
            .Select(m => m.UserId)
            .ToListAsync(ct);

        foreach (var userId in memberUserIds)
        {
            _db.Notifications.Add(Notification.Create(
                req.SpaceId, userId,
                "schedule.published",
                "סידור חדש פורסם",
                $"סידור גרסה {version.VersionNumber} פורסם ומוכן לצפייה.",
                System.Text.Json.JsonSerializer.Serialize(new { versionId = req.VersionId })));
        }
        await _db.SaveChangesAsync(ct);

        // Audit log — do this BEFORE the fire-and-forget so it uses the same DbContext
        // sequentially (no concurrency risk).
        await _audit.LogAsync(
            req.SpaceId, req.RequestingUserId,
            "publish_schedule",
            "schedule_version", req.VersionId,
            afterJson: $"{{\"version_number\":{version.VersionNumber}}}",
            ct: ct);

        // Send WhatsApp/email notifications to group members (fire-and-forget, non-blocking).
        // IMPORTANT: uses a NEW scope + DbContext so it doesn't race with the current request's
        // DbContext instance (EF Core DbContext is not thread-safe).
        if (_scheduleNotifications is not null)
        {
            var versionNumber = version.VersionNumber;
            _ = Task.Run(async () =>
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var notificationSender = scope.ServiceProvider.GetRequiredService<IScheduleNotificationSender>();
                await SendExternalNotificationsAsync(req, versionNumber, db, notificationSender, CancellationToken.None);
            });
        }
    }

    /// <summary>
    /// Sends WhatsApp/email notifications to all group members who have a phone or email.
    /// Runs fire-and-forget so it doesn't block the publish response.
    /// Uses its own DbContext scope — never shares the request's DbContext.
    /// </summary>
    private async Task SendExternalNotificationsAsync(
        PublishVersionCommand req, int versionNumber,
        AppDbContext db, IScheduleNotificationSender notificationSender,
        CancellationToken ct)
    {
        try
        {
            // Set RLS for the new scope's DbContext
            if (db.Database.IsRelational())
            {
                await db.Database.ExecuteSqlRawAsync(
                    "SELECT set_config('app.current_space_id', {0}, TRUE)",
                    req.SpaceId.ToString());
            }

            var frontendUrl = _config["App:FrontendBaseUrl"] ?? "https://shifter.app";

            // Load all people in this space who have a linked user (registered members)
            var members = await db.People.AsNoTracking()
                .Where(p => p.SpaceId == req.SpaceId && p.IsActive && p.LinkedUserId != null)
                .ToListAsync(ct);

            var userIds = members.Select(p => p.LinkedUserId!.Value).ToList();
            var users = await db.Users.AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, ct);

            // Get the space name for the notification message
            var space = await db.Spaces.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == req.SpaceId, ct);
            var spaceName = space?.Name ?? "Shifter";

            var scheduleUrl = $"{frontendUrl}/groups";

            foreach (var person in members)
            {
                if (person.LinkedUserId is null) continue;
                if (!users.TryGetValue(person.LinkedUserId.Value, out var user)) continue;

                // Prefer phone (WhatsApp), fall back to email
                var contact = !string.IsNullOrWhiteSpace(person.PhoneNumber)
                    ? person.PhoneNumber
                    : user.Email;

                if (string.IsNullOrWhiteSpace(contact)) continue;

                try
                {
                    await notificationSender.SendSchedulePublishedAsync(
                        contact,
                        person.DisplayName ?? person.FullName,
                        spaceName,
                        scheduleUrl,
                        ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Failed to send schedule publish notification to {Contact}", contact);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error sending external notifications for published schedule version {VersionId}",
                req.VersionId);
        }
    }
}
