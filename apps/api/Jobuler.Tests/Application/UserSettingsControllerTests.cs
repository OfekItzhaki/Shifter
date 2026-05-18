// Task 4.2: Unit tests for UserSettingsController
// Validates: Requirements 2.3, 2.4, 2.5

using System.Security.Claims;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Jobuler.Api.Controllers;
using Jobuler.Application.Common;
using Jobuler.Application.UserSettings.Commands;
using Jobuler.Application.UserSettings.Queries;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Jobuler.Tests.Application;

public class UserSettingsControllerTests
{
    private readonly IMediator _mediator;
    private readonly UserSettingsController _controller;
    private readonly Guid _userId = Guid.NewGuid();

    public UserSettingsControllerTests()
    {
        _mediator = Substitute.For<IMediator>();
        _controller = new UserSettingsController(_mediator);
        SetAuthenticatedUser(_userId);
    }

    private void SetAuthenticatedUser(Guid userId)
    {
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    private void SetUnauthenticatedUser()
    {
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() }
        };
    }

    // ── Valid Update Tests ────────────────────────────────────────────────────

    /// <summary>
    /// Validates: Requirement 2.5
    /// A valid country/state update returns 200 with the resolved timezone.
    /// </summary>
    [Fact]
    public async Task UpdateLocation_ValidCountryAndState_Returns200WithTimezoneResolution()
    {
        // Arrange
        var resolution = new TimezoneResolution("America/New_York", -300);
        _mediator.Send(Arg.Any<UpdateUserLocationCommand>(), Arg.Any<CancellationToken>())
            .Returns(resolution);

        var request = new UpdateUserLocationRequest("US", "NY");

        // Act
        var result = await _controller.UpdateLocation(request, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        // Verify the response contains timezone data
        var responseJson = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
        responseJson.Should().Contain("America/New_York");
        responseJson.Should().Contain("-300");
    }

    /// <summary>
    /// Validates: Requirement 2.5
    /// A valid single-timezone country (no state) returns 200 with resolved timezone.
    /// </summary>
    [Fact]
    public async Task UpdateLocation_ValidCountryNoState_Returns200WithTimezoneResolution()
    {
        // Arrange
        var resolution = new TimezoneResolution("Asia/Jerusalem", 120);
        _mediator.Send(Arg.Any<UpdateUserLocationCommand>(), Arg.Any<CancellationToken>())
            .Returns(resolution);

        var request = new UpdateUserLocationRequest("IL", null);

        // Act
        var result = await _controller.UpdateLocation(request, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        var responseJson = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
        responseJson.Should().Contain("Asia/Jerusalem");
        responseJson.Should().Contain("120");
    }

    /// <summary>
    /// Validates: Requirement 2.5
    /// The command sent to MediatR contains the correct user ID and location data.
    /// </summary>
    [Fact]
    public async Task UpdateLocation_SendsCorrectCommandToMediator()
    {
        // Arrange
        _mediator.Send(Arg.Any<UpdateUserLocationCommand>(), Arg.Any<CancellationToken>())
            .Returns(new TimezoneResolution("Europe/Berlin", 60));

        var request = new UpdateUserLocationRequest("DE", null);

        // Act
        await _controller.UpdateLocation(request, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<UpdateUserLocationCommand>(cmd =>
                cmd.UserId == _userId &&
                cmd.CountryCode == "DE" &&
                cmd.StateCode == null),
            Arg.Any<CancellationToken>());
    }

    // ── Invalid Country Code Tests ───────────────────────────────────────────

    /// <summary>
    /// Validates: Requirement 2.3
    /// An invalid country code causes the validation pipeline to throw ValidationException,
    /// which the ExceptionHandlingMiddleware converts to 400.
    /// Here we verify the controller propagates the exception (middleware handles HTTP status).
    /// </summary>
    [Fact]
    public async Task UpdateLocation_InvalidCountryCode_ThrowsValidationException()
    {
        // Arrange
        var validationFailures = new[]
        {
            new ValidationFailure("CountryCode", "Invalid ISO 3166-1 alpha-2 country code.")
        };
        _mediator.Send(Arg.Any<UpdateUserLocationCommand>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ValidationException(validationFailures));

        var request = new UpdateUserLocationRequest("XX", null);

        // Act
        var act = () => _controller.UpdateLocation(request, CancellationToken.None);

        // Assert
        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Errors.Should().Contain(e =>
            e.PropertyName == "CountryCode" &&
            e.ErrorMessage.Contains("Invalid ISO 3166-1 alpha-2"));
    }

    /// <summary>
    /// Validates: Requirement 2.3
    /// An empty country code triggers validation failure.
    /// </summary>
    [Fact]
    public async Task UpdateLocation_EmptyCountryCode_ThrowsValidationException()
    {
        // Arrange
        var validationFailures = new[]
        {
            new ValidationFailure("CountryCode", "Country code is required.")
        };
        _mediator.Send(Arg.Any<UpdateUserLocationCommand>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ValidationException(validationFailures));

        var request = new UpdateUserLocationRequest("", null);

        // Act
        var act = () => _controller.UpdateLocation(request, CancellationToken.None);

        // Assert
        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Errors.Should().Contain(e =>
            e.PropertyName == "CountryCode" &&
            e.ErrorMessage.Contains("required"));
    }

    // ── Invalid State Code Tests ─────────────────────────────────────────────

    /// <summary>
    /// Validates: Requirement 2.4
    /// A state code that doesn't belong to the selected country triggers validation failure.
    /// </summary>
    [Fact]
    public async Task UpdateLocation_InvalidStateCodeForCountry_ThrowsValidationException()
    {
        // Arrange
        var validationFailures = new[]
        {
            new ValidationFailure("StateCode", "Subdivision code does not belong to the selected country.")
        };
        _mediator.Send(Arg.Any<UpdateUserLocationCommand>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ValidationException(validationFailures));

        var request = new UpdateUserLocationRequest("US", "INVALID");

        // Act
        var act = () => _controller.UpdateLocation(request, CancellationToken.None);

        // Assert
        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Errors.Should().Contain(e =>
            e.PropertyName == "StateCode" &&
            e.ErrorMessage.Contains("does not belong to the selected country"));
    }

    /// <summary>
    /// Validates: Requirement 2.4
    /// A state code provided for a country that has no subdivisions triggers validation failure.
    /// </summary>
    [Fact]
    public async Task UpdateLocation_StateCodeForSingleTimezoneCountry_ThrowsValidationException()
    {
        // Arrange
        var validationFailures = new[]
        {
            new ValidationFailure("StateCode", "Subdivision code does not belong to the selected country.")
        };
        _mediator.Send(Arg.Any<UpdateUserLocationCommand>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ValidationException(validationFailures));

        var request = new UpdateUserLocationRequest("IL", "HA");

        // Act
        var act = () => _controller.UpdateLocation(request, CancellationToken.None);

        // Assert
        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Errors.Should().Contain(e =>
            e.PropertyName == "StateCode");
    }

    // ── Unauthenticated Access Tests ─────────────────────────────────────────

    /// <summary>
    /// Validates: Requirement 2.5
    /// The [Authorize] attribute is present on the controller, ensuring unauthenticated
    /// requests are rejected by the framework before reaching the action.
    /// We verify the attribute is applied correctly.
    /// </summary>
    [Fact]
    public void Controller_HasAuthorizeAttribute()
    {
        // Assert
        var controllerType = typeof(UserSettingsController);
        var authorizeAttr = controllerType
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true);

        authorizeAttr.Should().NotBeEmpty(
            "UserSettingsController must have [Authorize] to reject unauthenticated requests");
    }

    /// <summary>
    /// Validates: Requirement 2.5
    /// When no authenticated user is present, accessing CurrentUserId throws,
    /// simulating what happens when the auth middleware is bypassed.
    /// </summary>
    [Fact]
    public async Task UpdateLocation_UnauthenticatedUser_ThrowsWhenAccessingUserId()
    {
        // Arrange
        SetUnauthenticatedUser();
        var request = new UpdateUserLocationRequest("US", "NY");

        // Act
        var act = () => _controller.UpdateLocation(request, CancellationToken.None);

        // Assert — without a valid NameIdentifier claim, parsing the user ID will throw
        await act.Should().ThrowAsync<Exception>();
    }

    /// <summary>
    /// Validates: Requirement 2.5
    /// GetSettings also requires authentication — verify attribute coverage.
    /// </summary>
    [Fact]
    public async Task GetSettings_UnauthenticatedUser_ThrowsWhenAccessingUserId()
    {
        // Arrange
        SetUnauthenticatedUser();

        // Act
        var act = () => _controller.GetSettings(CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<Exception>();
    }

    // ── GetSettings Tests ────────────────────────────────────────────────────

    /// <summary>
    /// Validates: Requirement 6.5
    /// GetSettings returns the user's current settings including timezone info.
    /// </summary>
    [Fact]
    public async Task GetSettings_AuthenticatedUser_Returns200WithSettings()
    {
        // Arrange
        var dto = new UserSettingsDto("US", "CA", "America/Los_Angeles", -480, "24h");
        _mediator.Send(Arg.Any<GetUserSettingsQuery>(), Arg.Any<CancellationToken>())
            .Returns(dto);

        // Act
        var result = await _controller.GetSettings(CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(dto);
    }

    /// <summary>
    /// GetSettings sends the correct user ID to the query.
    /// </summary>
    [Fact]
    public async Task GetSettings_SendsCorrectQueryToMediator()
    {
        // Arrange
        _mediator.Send(Arg.Any<GetUserSettingsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new UserSettingsDto(null, null, "Asia/Jerusalem", 120, "24h"));

        // Act
        await _controller.GetSettings(CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<GetUserSettingsQuery>(q => q.UserId == _userId),
            Arg.Any<CancellationToken>());
    }
}
