using MediatR;

namespace Jobuler.Application.Billing.Commands;

public record DeactivateCouponCommand(Guid UserId, Guid CouponId) : IRequest;
