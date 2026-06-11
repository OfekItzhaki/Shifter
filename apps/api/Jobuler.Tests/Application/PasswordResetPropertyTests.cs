// Feature: group-alerts-and-phone
// Property tests for ForgotPasswordCommand / ResetPasswordCommand

using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Jobuler.Application.Auth;
using Jobuler.Application.Auth.Commands;
using Jobuler.Application.Common;
using Jobuler.Domain.Identity;
using Jobuler.Infrastructure.Persistence;
using Jobuler.Tests;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Jobuler.Tests.Application;

public class PasswordResetPropertyTests
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
    /// Returns a real SHA-256 hex hash of <paramref name="raw"/>, matching the
    /// production implementation in JwtService.
    /// </summary>
    private static string Sha256Hex(string raw)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// Builds an <see cref="IJwtService"/> mock whose <c>GenerateRefreshTokenRaw()</c>
    /// returns a fresh 64-char hex string on every call, and whose <c>HashToken(raw)</c>
    /// returns <c>SHA256(raw)</c> — matching the real implementation.
    /// </summary>
    private static IJwtService BuildJwtService()
    {
        var svc = Substitute.For<IJwtService>();
        svc.GenerateRefreshTokenRaw().Returns(_ => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant());
        svc.HashToken(Arg.Any<string>()).Returns(ci => Sha256Hex(ci.Arg<string>()));
        return svc;
    }

    /// <summary>
    /// Seeds an active <see cref="User"/> and returns it.
    /// </summary>
    private static async Task<User> SeedUser(AppDbContext db, string email = "user@example.com")
    {
        var user = User.Create(email, "Test User", BCrypt.Net.BCrypt.HashPassword("OldPass1!", workFactor: 4));
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    /// <summary>
    /// Runs <see cref="ForgotPasswordCommand"/> and returns the raw token that was
    /// delivered to <see cref="INotificationSender"/>.
    /// </summary>
    private static async Task<string> RunForgotPassword(
        AppDbContext db, IJwtService jwt, string email)
    {
        string capturedToken = string.Empty;
        var notifications = Substitute.For<INotificationSender>();
        notifications
            .SendPasswordResetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                capturedToken = ci.ArgAt<string>(1);
                return Task.CompletedTask;
            });

        var handler = new ForgotPasswordCommandHandler(db, notifications, jwt, TestContactLookupProtector.Create());
        await handler.Handle(new ForgotPasswordCommand(email), CancellationToken.None);
        return capturedToken;
    }

    // ── Property 13: User enumeration prevention ──────────────────────────────
    // Validates: Requirements 12.3

    [Theory]
    [InlineData("nobody@example.com")]
    [InlineData("ghost@test.org")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    public async Task Property13_ForgotPassword_UnknownEmail_CompletesWithoutException(string email)
    {
        // Arrange
        var db = CreateDb();
        var jwt = BuildJwtService();
        // No users seeded — every email is unknown

        // Act
        var act = async () => await RunForgotPassword(db, jwt, email);

        // Assert — must not throw
        await act.Should().NotThrowAsync();

        // No token row must be created
        db.PasswordResetTokens.Count().Should().Be(0);
    }

    [Theory]
    [InlineData("real@example.com", "other@example.com")]
    [InlineData("alice@test.org", "bob@test.org")]
    public async Task Property13_ForgotPassword_NonMatchingEmail_CreatesNoToken(
        string seededEmail, string requestEmail)
    {
        // Arrange
        var db = CreateDb();
        var jwt = BuildJwtService();
        await SeedUser(db, seededEmail);

        // Act — request with a different email
        var act = async () => await RunForgotPassword(db, jwt, requestEmail);

        // Assert
        await act.Should().NotThrowAsync();
        db.PasswordResetTokens.Count().Should().Be(0);
    }

    // ── Property 14: At most one active token per user ────────────────────────
    // Validates: Requirements 12.4

    [Fact]
    public async Task Property14_ForgotPassword_CalledTwice_ExactlyOneActiveToken()
    {
        // Arrange
        var db = CreateDb();
        var jwt = BuildJwtService();
        var user = await SeedUser(db);

        // Act — call twice
        await RunForgotPassword(db, jwt, user.Email);
        await RunForgotPassword(db, jwt, user.Email);

        // Assert — exactly one row with used_at IS NULL
        var tokens = await db.PasswordResetTokens.ToListAsync();
        tokens.Should().HaveCount(2);

        var activeTokens = tokens.Where(t => t.UsedAt == null).ToList();
        activeTokens.Should().HaveCount(1, "exactly one token should be active after two requests");

        var usedTokens = tokens.Where(t => t.UsedAt != null).ToList();
        usedTokens.Should().HaveCount(1, "the first token should have been marked used");
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    public async Task Property14_ForgotPassword_CalledNTimes_ExactlyOneActiveToken(int callCount)
    {
        // Arrange
        var db = CreateDb();
        var jwt = BuildJwtService();
        var user = await SeedUser(db);

        // Act
        for (int i = 0; i < callCount; i++)
            await RunForgotPassword(db, jwt, user.Email);

        // Assert
        var tokens = await db.PasswordResetTokens.ToListAsync();
        tokens.Should().HaveCount(callCount);
        tokens.Count(t => t.UsedAt == null).Should().Be(1,
            "only the most recent token should be active");
        tokens.Count(t => t.UsedAt != null).Should().Be(callCount - 1,
            "all previous tokens should be marked used");
    }

    // ── Property 12: Reset token hash is SHA-256 of raw token ─────────────────
    // Validates: Requirements 12.2

    [Fact]
    public async Task Property12_ForgotPassword_StoredHashIsSha256OfDeliveredToken()
    {
        // Arrange
        var db = CreateDb();
        var jwt = BuildJwtService();
        var user = await SeedUser(db);

        // Act
        var rawToken = await RunForgotPassword(db, jwt, user.Email);

        // Assert — token was actually delivered
        rawToken.Should().NotBeNullOrEmpty("INotificationSender must receive the raw token");

        var storedToken = await db.PasswordResetTokens.SingleAsync();

        // stored hash must equal SHA-256 of the raw token
        var expectedHash = Sha256Hex(rawToken);
        storedToken.TokenHash.Should().Be(expectedHash,
            "token_hash must be SHA-256 of the raw token delivered to INotificationSender");
    }

    [Fact]
    public async Task Property12_ForgotPassword_ExpiresAt_IsOneHourAfterCreatedAt()
    {
        // Arrange
        var db = CreateDb();
        var jwt = BuildJwtService();
        var user = await SeedUser(db);

        // Act
        await RunForgotPassword(db, jwt, user.Email);

        // Assert
        var storedToken = await db.PasswordResetTokens.SingleAsync();
        var expectedExpiry = storedToken.CreatedAt.AddHours(1);

        storedToken.ExpiresAt.Should().BeCloseTo(expectedExpiry, TimeSpan.FromSeconds(1),
            "expires_at must be within 1 second of created_at + 1 hour");
    }

    // ── Property 15: Invalid tokens are always rejected ───────────────────────
    // Validates: Requirements 14.3, 14.4, 14.5

    [Fact]
    public async Task Property15_ResetPassword_WrongHash_ThrowsInvalidOperation()
    {
        // Arrange
        var db = CreateDb();
        var jwt = BuildJwtService();
        var user = await SeedUser(db);
        var originalHash = user.PasswordHash;

        // Seed a valid token but submit a completely different raw token
        await RunForgotPassword(db, jwt, user.Email);
        var wrongRawToken = "this-is-not-the-right-token-" + Guid.NewGuid();

        var handler = new ResetPasswordCommandHandler(db, jwt);

        // Act & Assert
        var act = async () => await handler.Handle(
            new ResetPasswordCommand(wrongRawToken, "NewValidPass1!"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Invalid or expired reset token*");

        // Password must be unchanged
        var reloaded = await db.Users.FindAsync(user.Id);
        reloaded!.PasswordHash.Should().Be(originalHash);
    }

    [Fact]
    public async Task Property15_ResetPassword_ExpiredToken_ThrowsInvalidOperation()
    {
        // Arrange
        var db = CreateDb();
        var jwt = BuildJwtService();
        var user = await SeedUser(db);
        var originalHash = user.PasswordHash;

        // Seed a token then manually expire it
        var rawToken = await RunForgotPassword(db, jwt, user.Email);
        var token = await db.PasswordResetTokens.SingleAsync();

        // Force expiry by manipulating ExpiresAt via EF entry
        db.Entry(token).Property("ExpiresAt").CurrentValue = DateTime.UtcNow.AddHours(-2);
        await db.SaveChangesAsync();

        var handler = new ResetPasswordCommandHandler(db, jwt);

        // Act & Assert
        var act = async () => await handler.Handle(
            new ResetPasswordCommand(rawToken, "NewValidPass1!"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Invalid or expired reset token*");

        var reloaded = await db.Users.FindAsync(user.Id);
        reloaded!.PasswordHash.Should().Be(originalHash);
    }

    [Fact]
    public async Task Property15_ResetPassword_AlreadyUsedToken_ThrowsInvalidOperation()
    {
        // Arrange
        var db = CreateDb();
        var jwt = BuildJwtService();
        var user = await SeedUser(db);

        // First reset — succeeds
        var rawToken = await RunForgotPassword(db, jwt, user.Email);
        var handler = new ResetPasswordCommandHandler(db, jwt);
        await handler.Handle(new ResetPasswordCommand(rawToken, "NewValidPass1!"), CancellationToken.None);

        var passwordAfterFirstReset = (await db.Users.FindAsync(user.Id))!.PasswordHash;

        // Act — try to reuse the same token
        var act = async () => await handler.Handle(
            new ResetPasswordCommand(rawToken, "AnotherPass2@"), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Invalid or expired reset token*");

        // Password must not have changed again
        var reloaded = await db.Users.FindAsync(user.Id);
        reloaded!.PasswordHash.Should().Be(passwordAfterFirstReset);
    }

    // ── Property 16: BCrypt work factor 12 ────────────────────────────────────
    // Validates: Requirements 14.6

    [Theory]
    [InlineData("ValidPass1!")]
    [InlineData("AnotherSecure99#")]
    [InlineData("12345678")]          // exactly 8 chars — minimum valid
    [InlineData("LongPasswordWithManyCharacters123!@#")]
    public async Task Property16_ResetPassword_HashVerifiesWithBCrypt_WorkFactor12(string newPassword)
    {
        // Arrange
        var db = CreateDb();
        var jwt = BuildJwtService();
        var user = await SeedUser(db);

        var rawToken = await RunForgotPassword(db, jwt, user.Email);
        var handler = new ResetPasswordCommandHandler(db, jwt);

        // Act
        await handler.Handle(new ResetPasswordCommand(rawToken, newPassword), CancellationToken.None);

        // Assert
        var reloaded = await db.Users.FindAsync(user.Id);
        reloaded.Should().NotBeNull();

        BCrypt.Net.BCrypt.Verify(newPassword, reloaded!.PasswordHash)
            .Should().BeTrue("BCrypt.Verify must confirm the new password");

        reloaded.PasswordHash.Should().StartWith("$2a$12$",
            "work factor must be 12 as required by security rules");
    }

    // ── Property 17: Successful reset invalidates all refresh tokens ──────────
    // Validates: Requirements 14.8

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(10)]
    [InlineData(20)]
    public async Task Property17_ResetPassword_RevokesAllActiveRefreshTokens(int tokenCount)
    {
        // Arrange
        var db = CreateDb();
        var jwt = BuildJwtService();
        var user = await SeedUser(db);

        // Seed N active refresh tokens
        for (int i = 0; i < tokenCount; i++)
        {
            var rt = RefreshToken.Create(user.Id, Sha256Hex(Guid.NewGuid().ToString()), expiryDays: 7);
            db.RefreshTokens.Add(rt);
        }
        await db.SaveChangesAsync();

        var rawToken = await RunForgotPassword(db, jwt, user.Email);
        var handler = new ResetPasswordCommandHandler(db, jwt);

        // Act
        await handler.Handle(new ResetPasswordCommand(rawToken, "NewValidPass1!"), CancellationToken.None);

        // Assert — all refresh tokens must be revoked
        var refreshTokens = await db.RefreshTokens.Where(rt => rt.UserId == user.Id).ToListAsync();
        refreshTokens.Should().HaveCount(tokenCount);
        refreshTokens.Should().OnlyContain(rt => rt.RevokedAt != null,
            "all refresh tokens must have revoked_at set after a password reset");
    }

    [Fact]
    public async Task Property17_ResetPassword_NoRefreshTokens_Succeeds()
    {
        // Edge case: user has no refresh tokens — reset should still succeed
        var db = CreateDb();
        var jwt = BuildJwtService();
        var user = await SeedUser(db);

        var rawToken = await RunForgotPassword(db, jwt, user.Email);
        var handler = new ResetPasswordCommandHandler(db, jwt);

        var act = async () => await handler.Handle(
            new ResetPasswordCommand(rawToken, "NewValidPass1!"), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    // ── Property 18: Short passwords rejected ─────────────────────────────────
    // Validates: Requirements 14.7

    [Theory]
    [InlineData("")]          // length 0
    [InlineData("a")]         // length 1
    [InlineData("ab")]        // length 2
    [InlineData("abc")]       // length 3
    [InlineData("abcd")]      // length 4
    [InlineData("abcde")]     // length 5
    [InlineData("abcdef")]    // length 6
    [InlineData("abcdefg")]   // length 7
    public async Task Property18_ResetPassword_ShortPassword_ThrowsInvalidOperation(string shortPassword)
    {
        // Arrange
        var db = CreateDb();
        var jwt = BuildJwtService();
        var user = await SeedUser(db);
        var originalHash = user.PasswordHash;

        var rawToken = await RunForgotPassword(db, jwt, user.Email);
        var handler = new ResetPasswordCommandHandler(db, jwt);

        // Act & Assert
        var act = async () => await handler.Handle(
            new ResetPasswordCommand(rawToken, shortPassword), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*8 characters*");

        // Password must be unchanged
        var reloaded = await db.Users.FindAsync(user.Id);
        reloaded!.PasswordHash.Should().Be(originalHash,
            "password must not change when the new password is too short");
    }

    [Fact]
    public async Task Property18_ResetPassword_ExactlyEightChars_Succeeds()
    {
        // Boundary: exactly 8 characters must be accepted
        var db = CreateDb();
        var jwt = BuildJwtService();
        var user = await SeedUser(db);

        var rawToken = await RunForgotPassword(db, jwt, user.Email);
        var handler = new ResetPasswordCommandHandler(db, jwt);

        var act = async () => await handler.Handle(
            new ResetPasswordCommand(rawToken, "Exactly8"), CancellationToken.None);

        await act.Should().NotThrowAsync("8 characters is the minimum valid length");
    }
}
