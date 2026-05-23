using Jobuler.Application.Billing;

namespace Jobuler.Tests.Helpers;

/// <summary>
/// No-op peak member tracker for unit tests — does nothing.
/// </summary>
public class NoOpPeakMemberTracker : IPeakMemberTracker
{
    public Task TrackAsync(Guid spaceId, CancellationToken ct = default) => Task.CompletedTask;
}
