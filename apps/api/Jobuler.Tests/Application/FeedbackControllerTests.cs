using System.Security.Claims;
using FluentAssertions;
using Jobuler.Api.Controllers;
using Jobuler.Application.Feedback.Commands;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace Jobuler.Tests.Application;

public class FeedbackControllerTests
{
    [Fact]
    public async Task Submit_NormalizesTypeAndDescription_BeforeSendingCommand()
    {
        var mediator = Substitute.For<IMediator>();
        var controller = new FeedbackController(mediator);
        var userId = Guid.NewGuid();
        const string email = "user@example.com";

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                        new Claim(ClaimTypes.Email, email),
                    ],
                    "TestAuth"))
            }
        };

        var result = await controller.Submit(
            new SubmitFeedbackRequest(" Feedback ", "  This is helpful.  "),
            CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        await mediator.Received(1).Send(
            Arg.Is<SubmitFeedbackCommand>(command =>
                command.UserId == userId &&
                command.UserEmail == email &&
                command.Type == "feedback" &&
                command.Description == "This is helpful."),
            Arg.Any<CancellationToken>());
    }
}
