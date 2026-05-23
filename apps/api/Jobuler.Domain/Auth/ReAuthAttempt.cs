using Jobuler.Domain.Common;

namespace Jobuler.Domain.Auth;

/// <summary>
/// Records a single re-authentication attempt for lockout tracking and audit purposes.
/// Append-only — never updated or deleted.
/// </summary>
public class ReAuthAttempt : Entity
{
    public Guid UserId { get; private set; }
    public DateTime AttemptedAt { get; private set; }
    public bool Success { get; private set; }
    public string Method { get; private set; } = default!; // "password" | "webauthn"

    private ReAuthAttempt() { }

    public static ReAuthAttempt Create(Guid userId, bool success, string method)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));

        if (string.IsNullOrWhiteSpace(method))
            throw new ArgumentException("Method cannot be empty.", nameof(method));

        return new ReAuthAttempt
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AttemptedAt = DateTime.UtcNow,
            Success = success,
            Method = method
        };
    }
}
