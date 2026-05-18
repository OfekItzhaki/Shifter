using Jobuler.Application.Common;
using Jobuler.Application.Feedback.Exceptions;
using Jobuler.Domain.Feedback;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jobuler.Application.Feedback.Commands;

public class SubmitFeedbackCommandHandler : IRequestHandler<SubmitFeedbackCommand>
{
    private readonly AppDbContext _db;
    private readonly IEmailSender _emailSender;
    private readonly FeedbackOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SubmitFeedbackCommandHandler> _logger;

    public SubmitFeedbackCommandHandler(
        AppDbContext db,
        IEmailSender emailSender,
        IOptions<FeedbackOptions> options,
        TimeProvider timeProvider,
        ILogger<SubmitFeedbackCommandHandler> logger)
    {
        _db = db;
        _emailSender = emailSender;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task Handle(SubmitFeedbackCommand request, CancellationToken ct)
    {
        // Acquire a per-user advisory lock to prevent TOCTOU race conditions
        // in rate limiting. The lock is released when the transaction/connection ends.
        var lockKey = Math.Abs(request.UserId.GetHashCode());
        await _db.Database.ExecuteSqlRawAsync(
            "SELECT pg_advisory_xact_lock({0})", lockKey, ct);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var windowStart = now.AddMinutes(-60);

        var recentSubmissions = await _db.FeedbackSubmissions
            .Where(s => s.UserId == request.UserId && s.SubmittedAtUtc > windowStart)
            .OrderBy(s => s.SubmittedAtUtc)
            .ToListAsync(ct);

        if (recentSubmissions.Count >= _options.MaxSubmissionsPerHour)
        {
            var oldest = recentSubmissions.First();
            var retryAfter = (int)Math.Ceiling(
                (oldest.SubmittedAtUtc.AddMinutes(60) - now).TotalSeconds);
            throw new RateLimitExceededException(Math.Max(retryAfter, 1));
        }

        var escapedDescription = EscapeHtml(request.Description);

        var prefix = request.Type == "bug" ? "Bug Report: " : "Feedback: ";
        var descriptionForSubject = request.Description.Length > 50
            ? request.Description[..50]
            : request.Description;
        var subject = prefix + SanitizeSubject(descriptionForSubject);

        var htmlBody = $"""
            <p><strong>From:</strong> {EscapeHtml(request.UserEmail)}</p>
            <p><strong>Description:</strong></p>
            <p>{escapedDescription}</p>
            """;

        try
        {
            await _emailSender.SendAsync(_options.DeveloperEmail, subject, htmlBody, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "Failed to send feedback email for user {UserId}, type={Type}",
                request.UserId, request.Type);
            throw;
        }

        var submission = FeedbackSubmission.Create(request.UserId, now);
        _db.FeedbackSubmissions.Add(submission);
        await _db.SaveChangesAsync(ct);
    }

    private static string EscapeHtml(string input)
    {
        return input
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#x27;");
    }

    /// <summary>
    /// Strips CR/LF characters from the email subject to prevent header injection.
    /// </summary>
    private static string SanitizeSubject(string input)
    {
        return input.Replace("\r", "").Replace("\n", "");
    }
}
