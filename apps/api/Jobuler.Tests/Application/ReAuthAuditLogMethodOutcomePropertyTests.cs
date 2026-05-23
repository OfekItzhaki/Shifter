// Feature: admin-reauth-security
// Property 4: Audit log method and outcome correctness
// Validates: Requirements 6.1, 6.2, 6.4

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
/// Property 4: Audit log method and outcome correctness
/// For any re-authentication attempt (password or WebAuthn, success or failure),
/// the audit log entry correctly records the method and outcome.
/// </summary>
public class ReAuthAuditLogMethodOutcomePropertyTests
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

    // ── Property Tests ────────────────────────────────────────────────────────

    /// <summary>
    /// Property 4: For any password re-authentication attempt (success or failure),
    /// the audit log entry correctly records method="password" and the actual outcome.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Property4_PasswordReAuth_AuditLogRecordsCorrectMethodAndOutcome()
    {
        return Prop.ForAll(
            Arb.From<bool>(),
            success =>
            {
                // Arrange
                var db = CreateDb();
                var correctPassword = "CorrectPass123!";
                var user = SeedUser(db, correctPassword);
                var userId = user.Id;

                var webAuthn = Substitute.For<IWebAuthnService>();
                var (audit, captures) = BuildAuditLogger();
                var logger = Substitute.For<ILogger<ReAuthenticateCommandHandler>>();

                var handler = new ReAuthenticateCommandHandler(db, webAuthn, audit, logger);

                // Use correct or wrong password based on desired outcome
                var password = success ? correctPassword : "WrongPassword!";

                var command = new ReAuthenticateCommand(
                    UserId: userId,
                    Password: password,
                    WebAuthnChallengeId: null,
                    WebAuthnAssertionJson: null,
                    SpaceId: Guid.NewGuid(),
                    IpAddress: "192.168.1.1");

                // Act
                var result = handler.Handle(command, CancellationToken.None).GetAwaiter().GetResult();

                // Assert
                result.Success.Should().Be(success);

                // Find the re_authenticate audit log entry (not lockout)
                var reAuthEntry = captures.FirstOrDefault(c => c.Action == "re_authenticate");
                reAuthEntry.Should().NotBeNull("an audit log entry must be created for every re-auth attempt");

                var afterJson = JsonDocument.Parse(reAuthEntry!.AfterJson!);
                var root = afterJson.RootElement;

                root.GetProperty("method").GetString().Should().Be("password",
                    "audit log must record method as 'password' for password-based re-auth");
                root.GetProperty("success").GetBoolean().Should().Be(success,
                    "audit log must record the actual outcome (success/failure)");
            });
    }

    /// <summary>
    /// Property 4: For any WebAuthn re-authentication attempt (success or failure),
    /// the audit log entry correctly records method="webauthn" and the actual outcome.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Property4_WebAuthnReAuth_AuditLogRecordsCorrectMethodAndOutcome()
    {
        return Prop.ForAll(
            Arb.From<bool>(),
            success =>
            {
                // Arrange
                var db = CreateDb();
                var user = SeedUser(db, "SomePassword123!");
                var userId = user.Id;

                // Create a credential ID that matches what the assertion JSON will contain
                var credentialId = new byte[] { 10, 20, 30, 40, 50 };
                SeedWebAuthnCredential(db, userId, credentialId);

                var webAuthn = BuildWebAuthnService(success);
                var (audit, captures) = BuildAuditLogger();
                var logger = Substitute.For<ILogger<ReAuthenticateCommandHandler>>();

                var handler = new ReAuthenticateCommandHandler(db, webAuthn, audit, logger);

                // Build a minimal assertion JSON with the credential ID encoded as base64url
                var credIdBase64Url = Convert.ToBase64String(credentialId)
                    .Replace('+', '-').Replace('/', '_').TrimEnd('=');
                var assertionJson = JsonSerializer.Serialize(new { id = credIdBase64Url });

                var command = new ReAuthenticateCommand(
                    UserId: userId,
                    Password: null,
                    WebAuthnChallengeId: "challenge-123",
                    WebAuthnAssertionJson: assertionJson,
                    SpaceId: Guid.NewGuid(),
                    IpAddress: "10.0.0.1");

                // Act
                var result = handler.Handle(command, CancellationToken.None).GetAwaiter().GetResult();

                // Assert
                result.Success.Should().Be(success);

                // Find the re_authenticate audit log entry
                var reAuthEntry = captures.FirstOrDefault(c => c.Action == "re_authenticate");
                reAuthEntry.Should().NotBeNull("an audit log entry must be created for every re-auth attempt");

                var afterJson = JsonDocument.Parse(reAuthEntry!.AfterJson!);
                var root = afterJson.RootElement;

                root.GetProperty("method").GetString().Should().Be("webauthn",
                    "audit log must record method as 'webauthn' for WebAuthn-based re-auth");
                root.GetProperty("success").GetBoolean().Should().Be(success,
                    "audit log must record the actual outcome (success/failure)");
            });
    }

    /// <summary>
    /// Property 4 (combined): For any randomly generated (method, success) tuple,
    /// the handler produces an audit log entry with matching method and outcome.
    /// Uses FsCheck to generate 100+ random scenarios.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Property4_AnyReAuthAttempt_AuditLogMethodAndOutcomeMatchActual()
    {
        var gen = from method in Gen.Elements("password", "webauthn")
                  from success in Arb.Generate<bool>()
                  select (method, success);

        return Prop.ForAll(
            Arb.From(gen),
            scenario =>
            {
                var (method, success) = scenario;

                // Arrange
                var db = CreateDb();
                var correctPassword = "TestPassword99!";
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
                        SpaceId: Guid.NewGuid(),
                        IpAddress: "172.16.0.1");
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
                        SpaceId: Guid.NewGuid(),
                        IpAddress: "172.16.0.1");
                }

                // Act
                var result = handler.Handle(command, CancellationToken.None).GetAwaiter().GetResult();

                // Assert
                result.Success.Should().Be(success,
                    $"handler result should match expected outcome for method={method}");

                var reAuthEntry = captures.FirstOrDefault(c => c.Action == "re_authenticate");
                reAuthEntry.Should().NotBeNull(
                    "an audit log entry with action='re_authenticate' must be created");

                var afterJson = JsonDocument.Parse(reAuthEntry!.AfterJson!);
                var root = afterJson.RootElement;

                root.GetProperty("method").GetString().Should().Be(method,
                    $"audit log method must be '{method}'");
                root.GetProperty("success").GetBoolean().Should().Be(success,
                    $"audit log success must be {success} for method={method}");
            });
    }
}
