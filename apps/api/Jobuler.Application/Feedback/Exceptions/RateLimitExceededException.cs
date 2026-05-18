namespace Jobuler.Application.Feedback.Exceptions;

public class RateLimitExceededException : Exception
{
    public int RetryAfterSeconds { get; }

    public RateLimitExceededException(int retryAfterSeconds)
        : base("Rate limit exceeded.")
    {
        RetryAfterSeconds = retryAfterSeconds;
    }
}
