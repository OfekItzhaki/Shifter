using MediatR;

namespace Jobuler.Application.Billing.Commands;

public record CreateCouponCommand(
    Guid UserId, string Code, int DiscountPercent, int? MaxUses, DateTime? ValidUntil, string? Description
) : IRequest<CreateCouponResult>;

public record CreateCouponResult(Guid Id, string Code, int DiscountPercent);
