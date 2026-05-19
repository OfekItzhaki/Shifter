using FluentValidation;
using Jobuler.Application.HomeLeave.Queries;

namespace Jobuler.Application.HomeLeave.Validators;

public class GetFreezePeriodChangesCountQueryValidator : AbstractValidator<GetFreezePeriodChangesCountQuery>
{
    public GetFreezePeriodChangesCountQueryValidator()
    {
        RuleFor(x => x.SpaceId).NotEmpty();
        RuleFor(x => x.GroupId).NotEmpty();
        RuleFor(x => x.RequestingUserId).NotEmpty();
    }
}
