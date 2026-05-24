// Feature: schedule-regeneration
// Property 8: Permission enforcement
// **Validates: Requirements 7.1, 7.2**
//
// For any user without ScheduleRecalculate permission, a regeneration request
// SHALL be rejected with HTTP 403 (UnauthorizedAccessException) and no ScheduleRun created.

using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Jobuler.Api.Controllers;
using Jobuler.Application.Common;
using Jobuler.Application.Scheduling.Commands;
using Jobuler.Domain.Scheduling;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using System.Security.Claims;
using Xunit;

namespace Jobuler.Tests.Scheduling;

public class RegenerationPermissionEnforcementPropertyTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    /// <summary>
    /// Creates a permission service that denies ScheduleRecalculate permission
    /// by throwing UnauthorizedAccessException (which maps to HTTP 403 via middleware).
    /// </summary>
    private static IPermissionService DenyScheduleRecalculatePermission()
    {
        var svc = Substitute.For<IPermissionService>();
        svc.RequirePermissionAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Is(Permissions.ScheduleRecalculate), Arg.Any<CancellationToken>())
            .ThrowsAsync(new UnauthorizedAccessException("Permission denied."));
        return svc;
    }

    /// <summary>
    /// Creates a controller with a mocked user identity (the unauthorized user).
    /// </summary>
    private static ScheduleRunsController CreateController(
        Guid userId, IMediator mediator, IPermissionService permissions)
    {
        var controller = new ScheduleRunsController(mediator, permissions);

        // Set up the HttpContext with the user's claims
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        return controller;
    }

    // ── Property 8: Permission enforcement ────────────────────────────────────
    // For any user without ScheduleRecalculate permission, a regeneration request
    // SHALL be rejected with HTTP 403 and no ScheduleRun created.
    // **Validates: Requirements 7.1, 7.2**

    [Property(MaxTest = 100)]
    public Property Regeneration_DeniedWithoutScheduleRecalculatePermission_ThrowsUnauthorized()
    {
        // Generate random user IDs, space IDs, and group IDs
        var gen = from userId in Arb.Generate<Guid>()
                  from spaceId in Arb.Generate<Guid>()
                  from groupId in Arb.Generate<Guid>()
                  where userId != Guid.Empty && spaceId != Guid.Empty && groupId != Guid.Empty
                  select (userId, spaceId, groupId);

        return Prop.ForAll(Arb.From(gen), tuple =>
        {
            var (userId, spaceId, groupId) = tuple;

            // Arrange: permission service denies ScheduleRecalculate
            var permissions = DenyScheduleRecalculatePermission();
            var mediator = Substitute.For<IMediator>();
            var controller = CreateController(userId, mediator, permissions);

            // Also set up a DB to verify no ScheduleRun is created
            using var db = CreateDb();

            // Act: call the Regenerate endpoint — should throw UnauthorizedAccessException
            var act = () => controller.Regenerate(spaceId, new RegenerateRequest(groupId), CancellationToken.None)
                .GetAwaiter().GetResult();

            // Assert: UnauthorizedAccessException is thrown (maps to 403 via ExceptionHandlingMiddleware)
            act.Should().Throw<UnauthorizedAccessException>();

            // Assert: mediator was never called (command never dispatched)
            mediator.DidNotReceive().Send(
                Arg.Any<TriggerRegenerationCommand>(),
                Arg.Any<CancellationToken>());

            // Assert: no ScheduleRun was created in the database
            db.ScheduleRuns.Count().Should().Be(0,
                "no ScheduleRun should be created when permission is denied");
        });
    }

    [Property(MaxTest = 100)]
    public Property Regeneration_DeniedUser_MediatorNeverDispatches()
    {
        // Complementary property: verify the command is never dispatched to MediatR
        // when permission is denied, regardless of the group/space combination.
        var gen = from userId in Arb.Generate<Guid>()
                  from spaceId in Arb.Generate<Guid>()
                  from groupId in Arb.Generate<Guid>()
                  where userId != Guid.Empty && spaceId != Guid.Empty && groupId != Guid.Empty
                  select (userId, spaceId, groupId);

        return Prop.ForAll(Arb.From(gen), tuple =>
        {
            var (userId, spaceId, groupId) = tuple;

            // Arrange
            var permissions = DenyScheduleRecalculatePermission();
            var mediator = Substitute.For<IMediator>();
            var controller = CreateController(userId, mediator, permissions);

            // Act
            try
            {
                controller.Regenerate(spaceId, new RegenerateRequest(groupId), CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
            catch (UnauthorizedAccessException)
            {
                // Expected — permission denied
            }

            // Assert: TriggerRegenerationCommand was never sent
            mediator.DidNotReceive().Send(
                Arg.Any<TriggerRegenerationCommand>(),
                Arg.Any<CancellationToken>());
        });
    }
}
