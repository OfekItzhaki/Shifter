using MediatR;

namespace Jobuler.Application.Billing.Queries;

public record GetSpaceSubscriptionQuery(Guid SpaceId, Guid UserId) : IRequest<SpaceSubscriptionDto?>;

public record SpaceSubscriptionDto(
    string Status,
    string? TierId,
    DateTime? TrialStartsAt,
    DateTime? TrialEndsAt,
    DateTime? CurrentPeriodStart,
    DateTime? CurrentPeriodEnd,
    DateTime? CanceledAt,
    bool AutoRenew,
    bool IsActive,
    int? DaysRemaining);
