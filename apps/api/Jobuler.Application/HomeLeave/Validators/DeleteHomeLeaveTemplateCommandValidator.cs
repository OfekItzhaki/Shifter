using FluentValidation;
using Jobuler.Application.HomeLeave.Commands;

namespace Jobuler.Application.HomeLeave.Validators;

public class DeleteHomeLeaveTemplateCommandValidator : AbstractValidator<DeleteHomeLeaveTemplateCommand>
{
    public DeleteHomeLeaveTemplateCommandValidator()
    {
        RuleFor(x => x.SpaceId).NotEmpty();
        RuleFor(x => x.TemplateId).NotEmpty();
    }
}
