using MediatR;

namespace Jobuler.Application.Billing.Queries;

public record GetSpaceBillingAccessQuery(Guid SpaceId, Guid GroupId) : IRequest<bool>;
