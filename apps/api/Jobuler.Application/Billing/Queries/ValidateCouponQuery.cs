using MediatR;

namespace Jobuler.Application.Billing.Queries;

public record ValidateCouponQuery(string Code) : IRequest<CouponValidationResult>;

public record CouponValidationResult(bool Valid, int DiscountPercent);
