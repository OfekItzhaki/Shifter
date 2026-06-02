using FluentAssertions;
using Jobuler.Api.Controllers;
using Jobuler.Application.Auth.Commands;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Xunit;

namespace Jobuler.Tests.Application;

public class AuthCookieControllerTests
{
    [Fact]
    public async Task Login_SetsHttpOnlyRefreshCookie_AndOmitsRefreshTokenFromBody()
    {
        var loginResult = BuildLoginResult("refresh-login");
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<LoginCommand>(), Arg.Any<CancellationToken>())
            .Returns(loginResult);
        var context = CreateHttpsContext();
        var controller = CreateAuthController(mediator, context, Environments.Production);

        var result = await controller.Login(new LoginRequest(null, "user@example.com", "password"), CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().NotBeNull();
        ok.Value!.GetType().GetProperty("RefreshToken").Should().BeNull();

        var setCookie = context.Response.Headers.SetCookie.ToString();
        setCookie.Should().Contain("refresh_token=refresh-login");
        setCookie.Should().Contain("httponly", Exactly.Once());
        setCookie.Should().Contain("secure", Exactly.Once());
        setCookie.Should().Contain("samesite=strict", Exactly.Once());
        setCookie.Should().Contain("path=/auth", Exactly.Once());
    }

    [Fact]
    public async Task Refresh_UsesRefreshCookie_WhenBodyTokenIsMissing()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<RefreshTokenCommand>(), Arg.Any<CancellationToken>())
            .Returns(BuildLoginResult("refresh-rotated"));
        var context = CreateHttpsContext("refresh_token=cookie-refresh");
        var controller = CreateAuthController(mediator, context, Environments.Production);

        await controller.Refresh(null, CancellationToken.None);

        await mediator.Received(1).Send(
            Arg.Is<RefreshTokenCommand>(command => command.RefreshToken == "cookie-refresh"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Refresh_PrefersBodyToken_ForBackwardCompatibility()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<RefreshTokenCommand>(), Arg.Any<CancellationToken>())
            .Returns(BuildLoginResult("refresh-rotated"));
        var context = CreateHttpsContext("refresh_token=cookie-refresh");
        var controller = CreateAuthController(mediator, context, Environments.Production);

        await controller.Refresh(new RefreshRequest("body-refresh"), CancellationToken.None);

        await mediator.Received(1).Send(
            Arg.Is<RefreshTokenCommand>(command => command.RefreshToken == "body-refresh"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Logout_RevokesCookieRefreshToken_AndClearsCookie()
    {
        var mediator = Substitute.For<IMediator>();
        var context = CreateHttpsContext("refresh_token=cookie-refresh");
        var controller = CreateAuthController(mediator, context, Environments.Production);

        var result = await controller.Logout(null, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        await mediator.Received(1).Send(
            Arg.Is<RevokeTokenCommand>(command => command.RefreshToken == "cookie-refresh"),
            Arg.Any<CancellationToken>());

        var setCookie = context.Response.Headers.SetCookie.ToString();
        setCookie.Should().Contain("refresh_token=");
        setCookie.Should().Contain("max-age=0", Exactly.Once());
        setCookie.Should().Contain("httponly", Exactly.Once());
        setCookie.Should().Contain("path=/auth", Exactly.Once());
    }

    private static AuthController CreateAuthController(
        IMediator mediator,
        HttpContext context,
        string environmentName)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:RefreshTokenExpiryDays"] = "7"
            })
            .Build();
        var environment = Substitute.For<IWebHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);

        return new AuthController(mediator, configuration, environment)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = context
            }
        };
    }

    private static DefaultHttpContext CreateHttpsContext(string? cookieHeader = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        if (!string.IsNullOrWhiteSpace(cookieHeader))
        {
            context.Request.Headers.Cookie = cookieHeader;
        }

        return context;
    }

    private static LoginResult BuildLoginResult(string refreshToken) =>
        new(
            AccessToken: "access-token",
            RefreshToken: refreshToken,
            AccessTokenExpiresAt: DateTime.UtcNow.AddMinutes(15),
            UserId: Guid.NewGuid(),
            DisplayName: "Test User",
            PreferredLocale: "en",
            IsPlatformAdmin: false,
            TimezoneId: "Asia/Jerusalem",
            TimezoneOffsetMinutes: 120);
}
