using MediatR;

namespace Jobuler.Application.Billing.Queries;

public record ListCouponsQuery(Guid UserId) : IRequest<List<CouponDto>>;

public record CouponDto(
    Guid Id, string Code, int DiscountPercent, int? MaxUses, int CurrentUses,
    DateTime? ValidFrom, DateTime? ValidUntil, bool IsActive, string? Description
);
