using FluentValidation;
using Jobuler.Application.Scheduling.Models;

namespace Jobuler.Application.Scheduling.Validators;

/// <summary>
/// Validates the SimulateRequest payload before forwarding to the solver.
/// Ensures required fields are present and headcounts are non-negative.
/// </summary>
public class SimulateRequestValidator : AbstractValidator<SimulateRequest>
{
    public SimulateRequestValidator()
    {
        RuleFor(x => x.Payload).NotNull().WithMessage("Payload is required.");

        When(x => x.Payload != null, () =>
        {
            RuleFor(x => x.Payload.SpaceId).NotEmpty().WithMessage("SpaceId is required.");
            RuleFor(x => x.Payload.RunId).NotEmpty().WithMessage("RunId is required.");
            RuleFor(x => x.Payload.TriggerMode).NotEmpty().WithMessage("TriggerMode is required.");
            RuleFor(x => x.Payload.HorizonStart).NotEmpty().WithMessage("HorizonStart is required.");
            RuleFor(x => x.Payload.HorizonEnd).NotEmpty().WithMessage("HorizonEnd is required.");
            RuleFor(x => x.Payload.Locale).NotEmpty().WithMessage("Locale is required.");
            RuleFor(x => x.Payload.StabilityWeights).NotNull().WithMessage("StabilityWeights is required.");
            RuleFor(x => x.Payload.People).NotNull().WithMessage("People list is required.");
            RuleFor(x => x.Payload.TaskSlots).NotNull().WithMessage("TaskSlots list is required.");
            RuleFor(x => x.Payload.HardConstraints).NotNull().WithMessage("HardConstraints list is required.");
            RuleFor(x => x.Payload.SoftConstraints).NotNull().WithMessage("SoftConstraints list is required.");

            RuleForEach(x => x.Payload.TaskSlots).ChildRules(slot =>
            {
                slot.RuleFor(s => s.SlotId).NotEmpty().WithMessage("TaskSlot SlotId is required.");
                slot.RuleFor(s => s.RequiredHeadcount)
                    .GreaterThanOrEqualTo(0)
                    .WithMessage("RequiredHeadcount must be non-negative.");
                slot.RuleFor(s => s.StartsAt).NotEmpty().WithMessage("TaskSlot StartsAt is required.");
                slot.RuleFor(s => s.EndsAt).NotEmpty().WithMessage("TaskSlot EndsAt is required.");
            });
        });
    }
}
