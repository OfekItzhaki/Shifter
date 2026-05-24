using MediatR;

namespace Jobuler.Application.Billing.Commands;

public record SyncTrialDurationCommand() : IRequest;

public class SyncTrialDurationCommandHandler : IRequestHandler<SyncTrialDurationCommand>
{
    private readonly ITrialDurationCache _trialDurationCache;

    public SyncTrialDurationCommandHandler(ITrialDurationCache trialDurationCache)
    {
        _trialDurationCache = trialDurationCache;
    }

    public async Task Handle(SyncTrialDurationCommand request, CancellationToken ct)
    {
        await _trialDurationCache.SyncFromLemonSqueezyAsync(ct);
    }
}
