// Feature: admin-reauth-security
// Property 5: Audit log entry completeness
// Validates: Requirements 6.6

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
/// Property 5: Audit log entry completeness
/// For any re-authentication attempt, the audit log entry contains all required fields:
/// actor_user_id, space_id, action, entity_type, entity_id, IP address,
/// and an after-snapshot containing the authentication method and outcome.
/// </summary>
public class ReAuthAuditLogEntryCompletenessPropertyTests
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
    /// Seeds an active user with a known password hash.
    /// Uses low work factor (4) for test speed.
    /// </summary>
    private static User SeedUser(AppDbContext db, string password)
    {
        var hash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 4);
        var user = User.Create($"user-{Guid.NewGuid():N}@test.com", "Test User", hash);
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }

    /// <summary>
    /// Seeds a WebAuthn credential for the given user.
    /// </summary>
    private static WebAuthnCredential SeedWebAuthnCredential(AppDbContext db, Guid userId, byte[] credentialId)
    {
        var publicKey = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var credential = WebAuthnCredential.Create(
            userId, credentialId, publicKey, signCount: 1,
            transports: new[] { "internal" }, nickname: "Test Key");

        db.WebAuthnCredentials.Add(credential);
        db.SaveChanges();
        return credential;
    }

    /// <summary>
    /// Builds a mock IWebAuthnService that returns success or failure based on the parameter.
    /// </summary>
    private static IWebAuthnService BuildWebAuthnService(bool shouldSucceed)
    {
        var svc = Substitute.For<IWebAuthnService>();

        if (shouldSucceed)
        {
            svc.CompleteAuthenticationAsync(
                    Arg.Any<string>(), Arg.Any<string>(),
                    Arg.Any<byte[]>(), Arg.Any<uint>(), Arg.Any<CancellationToken>())
                .Returns(new AssertionVerificationResult(NewSignCount: 2, UserHandle: Array.Empty<byte>()));
        }
        else
        {
            svc.CompleteAuthenticationAsync(
                    Arg.Any<string>(), Arg.Any<string>(),
                    Arg.Any<byte[]>(), Arg.Any<uint>(), Arg.Any<CancellationToken>())
                .Returns<AssertionVerificationResult>(_ => throw new Exception("WebAuthn verification failed"));
        }

        return svc;
    }

    /// <summary>
    /// Captures all calls to IAuditLogger.LogAsync and returns the captured arguments.
    /// </summary>
    private static (IAuditLogger mock, List<AuditLogCapture> captures) BuildAuditLogger()
    {
        var captures = new List<AuditLogCapture>();
        var audit = Substitute.For<IAuditLogger>();

        audit.LogAsync(
                Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<string>(),
                Arg.Any<string?>(), Arg.Any<Guid?>(),
                Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                captures.Add(new AuditLogCapture(
                    SpaceId: ci.ArgAt<Guid?>(0),
                    ActorUserId: ci.ArgAt<Guid?>(1),
                    Action: ci.ArgAt<string>(2),
                    EntityType: ci.ArgAt<string?>(3),
                    EntityId: ci.ArgAt<Guid?>(4),
                    BeforeJson: ci.ArgAt<string?>(5),
                    AfterJson: ci.ArgAt<string?>(6),
                    IpAddress: ci.ArgAt<string?>(7)));
                return Task.CompletedTask;
            });

        return (audit, captures);
    }

    private record AuditLogCapture(
        Guid? SpaceId, Guid? ActorUserId, string Action,
        string? EntityType, Guid? EntityId,
        string? BeforeJson, string? AfterJson, string? IpAddress);

    // ── FsCheck Generators ────────────────────────────────────────────────────

    /// <summary>
    /// Generates a valid IPv4 address string (e.g. "192.168.1.42").
    /// </summary>
    private static Gen<string> GenIpAddress()
    {
        return from a in Gen.Choose(1, 255)
               from b in Gen.Choose(0, 255)
               from c in Gen.Choose(0, 255)
               from d in Gen.Choose(1, 254)
               select $"{a}.{b}.{c}.{d}";
    }

    /// <summary>
    /// Generates a random (userId, spaceId, ipAddress, method, success) tuple
    /// for property-based testing of audit log completeness.
    /// </summary>
    private static Arbitrary<(Guid UserId, Guid SpaceId, string IpAddress, string Method, bool Success)> GenReAuthScenario()
    {
        var gen = from spaceId in Arb.Generate<Guid>().Where(g => g != Guid.Empty)
                  from ipAddress in GenIpAddress()
                  from method in Gen.Elements("password", "webauthn")
                  from success in Arb.Generate<bool>()
                  select (UserId: Guid.Empty, spaceId, ipAddress, method, success);

        return Arb.From(gen);
    }

    // ── Property Tests ────────────────────────────────────────────────────────

    /// <summary>
    /// Property 5: For any re-authentication attempt, the audit log entry contains
    /// ALL required fields: actor_user_id matches userId, space_id matches spaceId,
    /// action is "re_authenticate", entity_type is "user", entity_id matches userId,
    /// IP address matches ipAddress, and afterJson contains method and success.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Property5_AuditLogEntry_ContainsAllRequiredFields()
    {
        var gen = from spaceId in Arb.Generate<Guid>().Where(g => g != Guid.Empty)
                  from ipAddress in GenIpAddress()
                  from method in Gen.Elements("password", "webauthn")
                  from success in Arb.Generate<bool>()
                  select (spaceId, ipAddress, method, success);

        return Prop.ForAll(
            Arb.From(gen),
            scenario =>
            {
                var (spaceId, ipAddress, method, success) = scenario;

                // Arrange
                var db = CreateDb();
                var correctPassword = "CorrectPass123!";
                var user = SeedUser(db, correctPassword);
                var userId = user.Id;

                var credentialId = new byte[] { 10, 20, 30, 40, 50 };
                SeedWebAuthnCredential(db, userId, credentialId);

                var webAuthn = BuildWebAuthnService(success);
                var (audit, captures) = BuildAuditLogger();
                var logger = Substitute.For<ILogger<ReAuthenticateCommandHandler>>();

                var handler = new ReAuthenticateCommandHandler(db, webAuthn, audit, logger);

                ReAuthenticateCommand command;

                if (method == "password")
                {
                    var password = success ? correctPassword : "WrongPassword!";
                    command = new ReAuthenticateCommand(
                        UserId: userId,
                        Password: password,
                        WebAuthnChallengeId: null,
                        WebAuthnAssertionJson: null,
                        SpaceId: spaceId,
                        IpAddress: ipAddress);
                }
                else
                {
                    var credIdBase64Url = Convert.ToBase64String(credentialId)
                        .Replace('+', '-').Replace('/', '_').TrimEnd('=');
                    var assertionJson = JsonSerializer.Serialize(new { id = credIdBase64Url });

                    command = new ReAuthenticateCommand(
                        UserId: userId,
                        Password: null,
                        WebAuthnChallengeId: "challenge-" + Guid.NewGuid(),
                        WebAuthnAssertionJson: assertionJson,
                        SpaceId: spaceId,
                        IpAddress: ipAddress);
                }

                // Act
                var result = handler.Handle(command, CancellationToken.None).GetAwaiter().GetResult();

                // Assert — verify the handler outcome matches expected
                result.Success.Should().Be(success,
                    $"handler result should match expected outcome for method={method}");

                // Find the re_authenticate audit log entry (not lockout)
                var reAuthEntry = captures.FirstOrDefault(c => c.Action == "re_authenticate");
                reAuthEntry.Should().NotBeNull(
                    "an audit log entry with action='re_authenticate' must be created for every re-auth attempt");

                // Verify actor_user_id
                reAuthEntry!.ActorUserId.Should().Be(userId,
                    "audit log actor_user_id must match the requesting user's ID");

                // Verify space_id
                reAuthEntry.SpaceId.Should().Be(spaceId,
                    "audit log space_id must match the space from the request");

                // Verify action
                reAuthEntry.Action.Should().Be("re_authenticate",
                    "audit log action must be 're_authenticate'");

                // Verify entity_type
                reAuthEntry.EntityType.Should().Be("user",
                    "audit log entity_type must be 'user'");

                // Verify entity_id
                reAuthEntry.EntityId.Should().Be(userId,
                    "audit log entity_id must match the user's ID");

                // Verify IP address
                reAuthEntry.IpAddress.Should().Be(ipAddress,
                    "audit log IP address must match the request's IP address");

                // Verify afterJson contains method and success
                reAuthEntry.AfterJson.Should().NotBeNullOrEmpty(
                    "audit log afterJson must not be null or empty");

                var afterDoc = JsonDocument.Parse(reAuthEntry.AfterJson!);
                var root = afterDoc.RootElement;

                root.TryGetProperty("method", out var methodProp).Should().BeTrue(
                    "afterJson must contain a 'method' property");
                methodProp.GetString().Should().Be(method,
                    $"afterJson method must be '{method}'");

                root.TryGetProperty("success", out var successProp).Should().BeTrue(
                    "afterJson must contain a 'success' property");
                successProp.GetBoolean().Should().Be(success,
                    $"afterJson success must be {success}");
            });
    }
}
