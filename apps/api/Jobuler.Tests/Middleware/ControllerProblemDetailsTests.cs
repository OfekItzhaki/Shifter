using FluentAssertions;
using Jobuler.Api.Controllers;
using Jobuler.Api.Middleware;
using Jobuler.Application.Common;
using Jobuler.Application.Notifications;
using Jobuler.Application.Scheduling.SelfService;
using Jobuler.Application.Scheduling.SelfService.Models;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using System.Security.Claims;
using Xunit;

namespace Jobuler.Tests.Middleware;

/// <summary>
/// Unit tests for controller-level ProblemDetails responses.
/// Validates Requirements 9.1, 9.2, 9.3.
/// </summary>
public class ControllerProblemDetailsTests
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly IPermissionService _permissions = Substitute.For<IPermissionService>();
    private readonly IShiftRequestService _shiftRequestService = Substitute.For<IShiftRequestService>();
    private readonly IWaitlistService _waitlistService = Substitute.For<IWaitlistService>();
    private readonly IPushNotificationSender _pushSender = Substitute.For<IPushNotificationSender>();

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static HttpContext CreateHttpContext(Guid userId)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/spaces/00000000-0000-0000-0000-000000000001/groups/00000000-0000-0000-0000-000000000002/shift-requests";
        httpContext.TraceIdentifier = "test-trace-id";

        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        httpContext.User = new ClaimsPrincipal(identity);

        return httpContext;
    }

    private ShiftRequestsController CreateShiftRequestsController(AppDbContext db, HttpContext httpContext)
    {
        var controller = new ShiftRequestsController(_mediator, _permissions, _shiftRequestService, _pushSender, db);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
        return controller;
    }

    private WaitlistController CreateWaitlistController(AppDbContext db, HttpContext httpContext)
    {
        var controller = new WaitlistController(_mediator, _permissions, _waitlistService, db);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
        return controller;
    }

    private async Task SeedPersonAsync(AppDbContext db, Guid spaceId, Guid groupId, Guid userId)
    {
        // Seed a linked person and group membership so member-scoped controller resolution succeeds.
        var person = Jobuler.Domain.People.Person.Create(spaceId, "Test User", displayName: null, linkedUserId: userId);
        db.People.Add(person);
        db.GroupMemberships.Add(Jobuler.Domain.Groups.GroupMembership.Create(spaceId, groupId, person.Id));
        await db.SaveChangesAsync();
    }

    // --- ShiftRequestsController Tests ---

    [Fact]
    public async Task Submit_WhenRejectedWithAlternativeSlots_ReturnsProblemDetailsWithAlternativeSlotsExtension()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var spaceId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var groupId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var db = CreateDb();
        await SeedPersonAsync(db, spaceId, groupId, userId);

        var httpContext = CreateHttpContext(userId);
        var controller = CreateShiftRequestsController(db, httpContext);

        var alternativeSlots = new List<AvailableSlotDto>
        {
            new(Guid.NewGuid(), new DateOnly(2025, 1, 15), new TimeOnly(8, 0), new TimeOnly(16, 0), "Morning Shift", 1, 3),
            new(Guid.NewGuid(), new DateOnly(2025, 1, 15), new TimeOnly(16, 0), new TimeOnly(0, 0), "Evening Shift", 0, 2)
        };

        var rejectionResult = new ShiftRequestResult(
            Success: false,
            ShiftRequestId: null,
            RejectionReason: "המשמרת מלאה.",
            AlternativeSlots: alternativeSlots);

        _shiftRequestService
            .ProcessRequestAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(rejectionResult);

        // Act
        var result = await controller.Submit(spaceId, groupId, new SubmitShiftRequestRequest(Guid.NewGuid()), CancellationToken.None);

        // Assert
        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(422);

        var problemDetails = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problemDetails.Title.Should().Be("Unprocessable Entity");
        problemDetails.Detail.Should().Be("המשמרת מלאה.");
        problemDetails.Type.Should().Be("https://docs.jobuler.com/errors/shift-request-rejected");
        problemDetails.Extensions.Should().ContainKey("alternativeSlots");
        problemDetails.Extensions["alternativeSlots"].Should().BeEquivalentTo(alternativeSlots);
    }

    [Fact]
    public async Task Submit_WhenRejectedWithoutAlternativeSlots_ReturnsProblemDetailsWithoutAlternativeSlotsExtension()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var spaceId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var groupId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var db = CreateDb();
        await SeedPersonAsync(db, spaceId, groupId, userId);

        var httpContext = CreateHttpContext(userId);
        var controller = CreateShiftRequestsController(db, httpContext);

        var rejectionResult = new ShiftRequestResult(
            Success: false,
            ShiftRequestId: null,
            RejectionReason: "חלון הבקשות סגור.",
            AlternativeSlots: null);

        _shiftRequestService
            .ProcessRequestAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(rejectionResult);

        // Act
        var result = await controller.Submit(spaceId, groupId, new SubmitShiftRequestRequest(Guid.NewGuid()), CancellationToken.None);

        // Assert
        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(422);

        var problemDetails = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problemDetails.Detail.Should().Be("חלון הבקשות סגור.");
        problemDetails.Extensions.Should().NotContainKey("alternativeSlots");
    }

    [Fact]
    public async Task Submit_WhenRejected_DetailMatchesRejectionReason()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var spaceId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var groupId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var db = CreateDb();
        await SeedPersonAsync(db, spaceId, groupId, userId);

        var httpContext = CreateHttpContext(userId);
        var controller = CreateShiftRequestsController(db, httpContext);

        const string rejectionReason = "הגעת למקסימום משמרות לשבוע.";
        var rejectionResult = new ShiftRequestResult(
            Success: false,
            ShiftRequestId: null,
            RejectionReason: rejectionReason,
            AlternativeSlots: null);

        _shiftRequestService
            .ProcessRequestAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(rejectionResult);

        // Act
        var result = await controller.Submit(spaceId, groupId, new SubmitShiftRequestRequest(Guid.NewGuid()), CancellationToken.None);

        // Assert
        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        var problemDetails = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problemDetails.Detail.Should().Be(rejectionReason);
    }

    // --- WaitlistController Tests ---

    [Fact]
    public async Task Join_WhenRejected_ReturnsProblemDetailsWith422AndCorrectDetail()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var spaceId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var groupId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var db = CreateDb();
        await SeedPersonAsync(db, spaceId, groupId, userId);

        var httpContext = CreateHttpContext(userId);
        httpContext.Request.Path = "/spaces/00000000-0000-0000-0000-000000000001/groups/00000000-0000-0000-0000-000000000002/waitlist";
        var controller = CreateWaitlistController(db, httpContext);

        const string errorMessage = "כבר נמצא ברשימת ההמתנה למשמרת זו.";
        var waitlistResult = new WaitlistResult(
            Success: false,
            Position: null,
            ErrorMessage: errorMessage);

        _waitlistService
            .JoinWaitlistAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(waitlistResult);

        _permissions
            .RequirePermissionAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        var result = await controller.Join(spaceId, groupId, new JoinWaitlistRequest(Guid.NewGuid()), CancellationToken.None);

        // Assert
        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(422);

        var problemDetails = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problemDetails.Title.Should().Be("Unprocessable Entity");
        problemDetails.Detail.Should().Be(errorMessage);
        problemDetails.Type.Should().Be("https://docs.jobuler.com/errors/waitlist-rejected");
        problemDetails.Extensions.Should().ContainKey("traceId");
    }

    [Fact]
    public async Task Join_WhenRejected_ProblemDetailsIncludesTraceIdAndInstance()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var spaceId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var groupId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var db = CreateDb();
        await SeedPersonAsync(db, spaceId, groupId, userId);

        var httpContext = CreateHttpContext(userId);
        httpContext.Request.Path = "/spaces/00000000-0000-0000-0000-000000000001/groups/00000000-0000-0000-0000-000000000002/waitlist";
        httpContext.TraceIdentifier = "custom-trace-123";
        var controller = CreateWaitlistController(db, httpContext);

        var waitlistResult = new WaitlistResult(
            Success: false,
            Position: null,
            ErrorMessage: "Slot is not full.");

        _waitlistService
            .JoinWaitlistAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(waitlistResult);

        _permissions
            .RequirePermissionAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        var result = await controller.Join(spaceId, groupId, new JoinWaitlistRequest(Guid.NewGuid()), CancellationToken.None);

        // Assert
        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        var problemDetails = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problemDetails.Instance.Should().Be("/spaces/00000000-0000-0000-0000-000000000001/groups/00000000-0000-0000-0000-000000000002/waitlist");
        problemDetails.Extensions["traceId"].Should().Be("custom-trace-123");
    }
}
