namespace Jobuler.Domain.Feedback;

/// <summary>
/// Lightweight entity used solely for per-user rate limiting of feedback/bug submissions.
/// Not tenant-scoped — feedback is per-user, not per-space.
/// </summary>
public class FeedbackSubmission
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public DateTime SubmittedAtUtc { get; private set; }

    private FeedbackSubmission() { }

    public static FeedbackSubmission Create(Guid userId, DateTime? submittedAtUtc = null)
    {
        return new FeedbackSubmission
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SubmittedAtUtc = submittedAtUtc ?? DateTime.UtcNow
        };
    }
}
