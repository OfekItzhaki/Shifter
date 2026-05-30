using FluentValidation;
using Jobuler.Application.Spaces.Commands;

namespace Jobuler.Application.Spaces.Validators;

public class JoinSpaceByInviteCodeCommandValidator : AbstractValidator<JoinSpaceByInviteCodeCommand>
{
    public JoinSpaceByInviteCodeCommandValidator()
    {
        RuleFor(x => x.InviteCode)
            .NotEmpty().WithMessage("Invite code is required.")
            .Length(8).WithMessage("Invite code must be exactly 8 characters.")
            .Matches("^[A-Za-z0-9]+$").WithMessage("Invite code must be alphanumeric.");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required.");
    }
}
