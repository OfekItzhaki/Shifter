using MediatR;

namespace Jobuler.Application.Feedback.Commands;

public record SubmitFeedbackCommand(
    Guid UserId,
    string UserEmail,
    string Type,
    string Description
) : IRequest;
