// Feature: admin-reauth-security, Property 5: Audit log entry completeness
// Validates: Requirements 6.6
//
// For any re-authentication attempt, the audit log entry contains:
// actor_user_id, space_id, action ("re_authenticate"), entity_type ("user"),
// entity_id, IP address, and after-snapshot with method and outcome.

using System.Text.Json;
using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Jobuler.Application.Auth;
using Jobuler.Application.Auth.Commands;
using Jobuler.Application.Common;
using Jobuler.Domain.Identity;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Jobuler.Tests.Application;

/// <summary>
/// Property 5: Audit log entry completeness.
/// For any re-authentication attempt, the resulting audit log entry SHALL contain
/// all required fields: actor_user_id, space_id, action, entity_type, entity_id,
/// IP address, and an after-snapshot containing the authentication method and outcome.
/// </summary>
public class ReAuthAuditLogCompletenessPropertyTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<User> SeedActiveUser(AppDbContext db, Guid userId)
    {
        var passwordHash = BCrypt.Net.BCrypt.HashPassword("TestPass1!", workFactor: 4);
        var user = User.Create($"user-{userId}@test.com", "Test User", passwordHash);

        // Use reflection to set the Id since it's inherited from Entity base
        var idProp = typeof(User).BaseType!.BaseType!.GetProperty("Id");
        idProp!.SetValue(user, userId);

        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    /// <summary>
    /// Generates a valid IP address string from 4 random bytes.
    /// </summary>
    private static string GenerateIpAddress(byte a, byte b, byte c, byte d)
        => $"{a}.{b}.{c}.{d}";

    // ── Property 5 ───────────────────────────────────────────────────────────

    [Property(MaxTest = 100, Arbitrary = new[] { typeof(ReAuthAuditCompletenessArbitrary) })]
    public async Task<bool> Property5_AuditLogEntry_ContainsAllRequiredFields(ReAuthAuditCompletenessInput input)
    {
        // Arrange
        var db = CreateDb();
        var user = await SeedActiveUser(db, input.UserId);

        var audit = Substitute.For<IAuditLogger>();
        var webAuthn = Substitute.For<IWebAuthnService>();
        var logger = Substitute.For<ILogger<ReAuthenticateCommandHandler>>();

        // Capture audit log call arguments
        Guid? capturedActorUserId = null;
        Guid? capturedSpaceId = null;
        string? capturedAction = null;
        string? capturedEntityType = null;
        Guid? capturedEntityId = null;
        string? capturedIpAddress = null;
        string? capturedAfterJson = null;

        audit.LogAsync(
            Arg.Any<Guid?>(),
            Arg.Any<Guid?>(),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<Guid?>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>()
        ).Returns(ci =>
        {
            capturedSpaceId = ci.ArgAt<Guid?>(0);
            capturedActorUserId = ci.ArgAt<Guid?>(1);
            capturedAction = ci.ArgAt<string>(2);
            capturedEntityType = ci.ArgAt<string?>(3);
            capturedEntityId = ci.ArgAt<Guid?>(4);
            // index 5 = beforeJson (not used here)
            capturedAfterJson = ci.ArgAt<string?>(6);
            capturedIpAddress = ci.ArgAt<string?>(7);
            return Task.CompletedTask;
        });

        var handler = new ReAuthenticateCommandHandler(db, webAuthn, audit, logger);

        // Build command — use password method for simplicity (always verifiable)
        var command = new ReAuthenticateCommand(
            UserId: input.UserId,
            Password: input.UseCorrectPassword ? "TestPass1!" : "WrongPassword!",
            WebAuthnChallengeId: null,
            WebAuthnAssertionJson: null,
            SpaceId: input.SpaceId,
            IpAddress: input.IpAddress
        );

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert — all required fields must be present
        capturedActorUserId.Should().Be(input.UserId, "actor_user_id must match the requesting user");
        capturedSpaceId.Should().Be(input.SpaceId, "space_id must match the request space");
        capturedAction.Should().Be("re_authenticate", "action must be 're_authenticate'");
        capturedEntityType.Should().Be("user", "entity_type must be 'user'");
        capturedEntityId.Should().Be(input.UserId, "entity_id must match the user ID");
        capturedIpAddress.Should().Be(input.IpAddress, "IP address must match the request IP");

        // Verify after-snapshot contains method and outcome
        capturedAfterJson.Should().NotBeNullOrEmpty("afterJson must be present");
        var afterDoc = JsonDocument.Parse(capturedAfterJson!);
        var root = afterDoc.RootElement;

        root.TryGetProperty("method", out var methodProp).Should().BeTrue("afterJson must contain 'method'");
        methodProp.GetString().Should().Be("password", "method must be 'password' for password-based auth");

        root.TryGetProperty("success", out var successProp).Should().BeTrue("afterJson must contain 'success'");
        var expectedSuccess = input.UseCorrectPassword;
        successProp.GetBoolean().Should().Be(expectedSuccess, "success must match the actual verification result");

        return true;
    }
}

/// <summary>
/// Input record for the audit log completeness property test.
/// </summary>
public record ReAuthAuditCompletenessInput(
    Guid UserId,
    Guid SpaceId,
    string IpAddress,
    bool UseCorrectPassword
);

/// <summary>
/// FsCheck arbitrary for generating valid ReAuthAuditCompletenessInput values.
/// </summary>
public class ReAuthAuditCompletenessArbitrary
{
    public static Arbitrary<ReAuthAuditCompletenessInput> Generate()
    {
        var gen = from userId in Gen.Fresh(() => Guid.NewGuid())
                  from spaceId in Gen.Fresh(() => Guid.NewGuid())
                  from a in Gen.Choose(1, 255)
                  from b in Gen.Choose(0, 255)
                  from c in Gen.Choose(0, 255)
                  from d in Gen.Choose(1, 254)
                  from useCorrectPassword in Arb.Generate<bool>()
                  select new ReAuthAuditCompletenessInput(
                      userId,
                      spaceId,
                      $"{a}.{b}.{c}.{d}",
                      useCorrectPassword
                  );

        return Arb.From(gen);
    }
}
