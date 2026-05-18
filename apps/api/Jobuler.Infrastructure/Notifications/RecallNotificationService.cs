using Jobuler.Application.Common;
using Jobuler.Application.HomeLeave.Services;
using Jobuler.Application.Notifications;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Jobuler.Infrastructure.Notifications;

/// <summary>
/// Sends recall notifications (push + email) to a person being recalled from home leave.
/// Push delivery retries up to 3 times with exponential backoff (1s, 2s, 4s).
/// Email failures are logged and do not block the recall operation.
/// </summary>
public class RecallNotificationService : IRecallNotificationService
{
    private readonly AppDbContext _db;
    private readonly IPushNotificationSender _pushSender;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<RecallNotificationService> _logger;

    private const int MaxRetries = 3;
    private static readonly TimeSpan[] RetryDelays = { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4) };

    public RecallNotificationService(
        AppDbContext db,
        IPushNotificationSender pushSender,
        IEmailSender emailSender,
        ILogger<RecallNotificationService> logger)
    {
        _db = db;
        _pushSender = pushSender;
        _emailSender = emailSender;
        _logger = logger;
    }

    public async Task<bool> SendRecallNotificationAsync(
        Guid spaceId,
        Guid recalledPersonId,
        string adminName,
        string? reason,
        DateTime? expectedReturnAt,
        CancellationToken ct = default)
    {
        // Resolve the person's linked user ID and email
        var personData = await _db.People.AsNoTracking()
            .Where(p => p.Id == recalledPersonId && p.SpaceId == spaceId)
            .Select(p => new { p.LinkedUserId })
            .FirstOrDefaultAsync(ct);

        if (personData?.LinkedUserId is null)
        {
            _logger.LogWarning(
                "Cannot send recall notification: Person {PersonId} in space {SpaceId} has no linked user",
                recalledPersonId, spaceId);
            return false;
        }

        var userId = personData.LinkedUserId.Value;

        var userEmail = await _db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.Email)
            .FirstOrDefaultAsync(ct);

        // Build notification payload
        var payload = BuildPushPayload(adminName, reason, expectedReturnAt);

        // Send push notification with retry logic
        var pushSuccess = await SendPushWithRetryAsync(userId, spaceId, payload, ct);

        // Send email — on failure, log and continue without blocking
        if (!string.IsNullOrWhiteSpace(userEmail))
        {
            await SendEmailSafeAsync(userEmail, adminName, reason, expectedReturnAt, ct);
        }
        else
        {
            _logger.LogWarning(
                "Cannot send recall email: User {UserId} has no email address",
                userId);
        }

        return pushSuccess;
    }

    private static PushPayload BuildPushPayload(string adminName, string? reason, DateTime? expectedReturnAt)
    {
        var bodyParts = new List<string> { $"You have been recalled from home leave by {adminName}." };

        if (!string.IsNullOrWhiteSpace(reason))
            bodyParts.Add($"Reason: {reason}");

        if (expectedReturnAt.HasValue)
            bodyParts.Add($"Expected return: {expectedReturnAt.Value:g}");

        return new PushPayload(
            Title: "Home Leave Recalled",
            Body: string.Join(" ", bodyParts),
            Tag: "home_leave_recall",
            Url: "/schedule");
    }

    private async Task<bool> SendPushWithRetryAsync(
        Guid userId, Guid spaceId, PushPayload payload, CancellationToken ct)
    {
        for (var attempt = 0; attempt <= MaxRetries; attempt++)
        {
            try
            {
                await _pushSender.SendPushToUserAsync(userId, spaceId, payload, ct);
                return true;
            }
            catch (Exception ex) when (attempt < MaxRetries)
            {
                _logger.LogWarning(ex,
                    "Push notification delivery attempt {Attempt} failed for user {UserId} in space {SpaceId}. Retrying in {Delay}s",
                    attempt + 1, userId, spaceId, RetryDelays[attempt].TotalSeconds);

                await Task.Delay(RetryDelays[attempt], ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Push notification delivery failed after {MaxRetries} retries for user {UserId} in space {SpaceId}",
                    MaxRetries, userId, spaceId);
                return false;
            }
        }

        return false;
    }

    private async Task SendEmailSafeAsync(
        string email, string adminName, string? reason,
        DateTime? expectedReturnAt, CancellationToken ct)
    {
        try
        {
            var subject = "Home Leave Recalled";
            var html = BuildEmailHtml(adminName, reason, expectedReturnAt);
            await _emailSender.SendAsync(email, subject, html, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send recall email to {Email}. Continuing without blocking recall operation.",
                email);
        }
    }

    private static string BuildEmailHtml(string adminName, string? reason, DateTime? expectedReturnAt)
    {
        var reasonSection = !string.IsNullOrWhiteSpace(reason)
            ? $"<p><strong>Reason:</strong> {System.Net.WebUtility.HtmlEncode(reason)}</p>"
            : "";

        var returnSection = expectedReturnAt.HasValue
            ? $"<p><strong>Expected return:</strong> {expectedReturnAt.Value:g}</p>"
            : "";

        return $"""
            <div style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;">
              <h2>Home Leave Recalled</h2>
              <p>You have been recalled from home leave by <strong>{System.Net.WebUtility.HtmlEncode(adminName)}</strong>.</p>
              {reasonSection}
              {returnSection}
              <p>Please check your schedule for further details.</p>
              <a href="/schedule"
                 style="display:inline-block;background:#dc2626;color:white;padding:12px 24px;
                        border-radius:8px;text-decoration:none;font-weight:bold;margin:16px 0;">
                View Schedule
              </a>
            </div>
            """;
    }
}
