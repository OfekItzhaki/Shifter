using MediatR;

namespace Jobuler.Application.Billing.Queries;

public record GetSubscriptionQuery(Guid SpaceId, Guid GroupId) : IRequest<SubscriptionDto?>;

public record SubscriptionDto(
    string Status,
    string? TierId,
    DateTime? TrialEndsAt,
    int PeakMemberCount,
    int DiscountPercent,
    string? CouponCode,
    bool IsActive
);
