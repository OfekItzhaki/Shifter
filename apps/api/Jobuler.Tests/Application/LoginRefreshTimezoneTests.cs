// Task 3.3: Unit tests for login and refresh timezone integration
// Validates: Requirements 4.1, 4.2, 4.4

using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Jobuler.Application.Auth;
using Jobuler.Application.Auth.Commands;
using Jobuler.Application.Common;
using Jobuler.Application.Conflicts;
using Jobuler.Domain.Identity;
using Jobuler.Infrastructure.Persistence;
using Jobuler.Tests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Jobuler.Tests.Application;

public class LoginRefreshTimezoneTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static string Sha256Hex(string raw)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static IJwtService BuildJwtService()
    {
        var svc = Substitute.For<IJwtService>();
        svc.GenerateAccessToken(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns("test-access-token");
        svc.GenerateRefreshTokenRaw()
            .Returns(_ => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant());
        svc.HashToken(Arg.Any<string>())
            .Returns(ci => Sha256Hex(ci.Arg<string>()));
        return svc;
    }

    private static IConfiguration BuildConfig()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:RefreshTokenExpiryDays"] = "7"
            })
            .Build();
        return config;
    }

    private static IServiceScopeFactory BuildScopeFactory()
    {
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        var scope = Substitute.For<IServiceScope>();
        var serviceProvider = Substitute.For<IServiceProvider>();
        var conflictService = Substitute.For<IConflictDetectionService>();
        var logger = Substitute.For<ILogger<LoginCommandHandler>>();

        serviceProvider.GetService(typeof(IConflictDetectionService)).Returns(conflictService);
        serviceProvider.GetService(typeof(ILogger<LoginCommandHandler>)).Returns(logger);
        scope.ServiceProvider.Returns(serviceProvider);
        scopeFactory.CreateScope().Returns(scope);

        return scopeFactory;
    }

    private static async Task<User> SeedUser(
        AppDbContext db,
        string email = "user@example.com",
        string password = "TestPass1!",
        string? countryCode = null,
        string? stateCode = null)
    {
        var user = User.Create(
            email, "Test User",
            BCrypt.Net.BCrypt.HashPassword(password, workFactor: 4));

        if (countryCode != null || stateCode != null)
            user.UpdateLocation(countryCode, stateCode);

        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    // ── Login Tests ──────────────────────────────────────────────────────────

    /// <summary>
    /// Validates: Requirement 4.1, 4.2
    /// Login response includes timezoneId and timezoneOffsetMinutes fields.
    /// </summary>
    [Fact]
    public async Task Login_ResponseIncludesTimezoneIdAndOffsetMinutes()
    {
        // Arrange
        var db = CreateDb();
        var jwt = BuildJwtService();
        var timezoneResolver = Substitute.For<ITimezoneResolver>();
        timezoneResolver.Resolve("US", "CA")
            .Returns(new TimezoneResolution("America/Los_Angeles", -480));

        var user = await SeedUser(db, countryCode: "US", stateCode: "CA");

        var handler = new LoginCommandHandler(
            db, jwt, timezoneResolver, TestContactLookupProtector.Create(), BuildConfig(), BuildScopeFactory(),
            Substitute.For<ILogger<LoginCommandHandler>>());

        // Act
        var result = await handler.Handle(
            new LoginCommand(user.Email, "TestPass1!"), CancellationToken.None);

        // Assert
        result.TimezoneId.Should().Be("America/Los_Angeles");
        result.TimezoneOffsetMinutes.Should().Be(-480);
    }

    /// <summary>
    /// Validates: Requirement 4.1, 4.2
    /// Login response includes timezone for a single-timezone country.
    /// </summary>
    [Fact]
    public async Task Login_SingleTimezoneCountry_ReturnsCorrectTimezone()
    {
        // Arrange
        var db = CreateDb();
        var jwt = BuildJwtService();
        var timezoneResolver = Substitute.For<ITimezoneResolver>();
        timezoneResolver.Resolve("IL", null)
            .Returns(new TimezoneResolution("Asia/Jerusalem", 120));

        var user = await SeedUser(db, countryCode: "IL");

        var handler = new LoginCommandHandler(
            db, jwt, timezoneResolver, TestContactLookupProtector.Create(), BuildConfig(), BuildScopeFactory(),
            Substitute.For<ILogger<LoginCommandHandler>>());

        // Act
        var result = await handler.Handle(
            new LoginCommand(user.Email, "TestPass1!"), CancellationToken.None);

        // Assert
        result.TimezoneId.Should().Be("Asia/Jerusalem");
        result.TimezoneOffsetMinutes.Should().Be(120);
    }

    /// <summary>
    /// Validates: Requirement 4.1, 4.2, 4.4 (fallback behavior)
    /// When user has no country set (null), defaults to Asia/Jerusalem.
    /// </summary>
    [Fact]
    public async Task Login_NullCountry_DefaultsToAsiaJerusalem()
    {
        // Arrange
        var db = CreateDb();
        var jwt = BuildJwtService();
        var timezoneResolver = Substitute.For<ITimezoneResolver>();
        timezoneResolver.Resolve(null, null)
            .Returns(new TimezoneResolution("Asia/Jerusalem", 120));

        var user = await SeedUser(db); // no country/state set

        var handler = new LoginCommandHandler(
            db, jwt, timezoneResolver, TestContactLookupProtector.Create(), BuildConfig(), BuildScopeFactory(),
            Substitute.For<ILogger<LoginCommandHandler>>());

        // Act
        var result = await handler.Handle(
            new LoginCommand(user.Email, "TestPass1!"), CancellationToken.None);

        // Assert
        result.TimezoneId.Should().Be("Asia/Jerusalem");
        result.TimezoneOffsetMinutes.Should().Be(120);
        timezoneResolver.Received(1).Resolve(null, null);
    }

    /// <summary>
    /// Validates: Requirement 4.1
    /// Login calls ITimezoneResolver.Resolve with the user's country and state codes.
    /// </summary>
    [Fact]
    public async Task Login_CallsTimezoneResolverWithUserLocation()
    {
        // Arrange
        var db = CreateDb();
        var jwt = BuildJwtService();
        var timezoneResolver = Substitute.For<ITimezoneResolver>();
        timezoneResolver.Resolve(Arg.Any<string?>(), Arg.Any<string?>())
            .Returns(new TimezoneResolution("Europe/London", 0));

        var user = await SeedUser(db, countryCode: "GB", stateCode: null);

        var handler = new LoginCommandHandler(
            db, jwt, timezoneResolver, TestContactLookupProtector.Create(), BuildConfig(), BuildScopeFactory(),
            Substitute.For<ILogger<LoginCommandHandler>>());

        // Act
        await handler.Handle(
            new LoginCommand(user.Email, "TestPass1!"), CancellationToken.None);

        // Assert
        timezoneResolver.Received(1).Resolve("GB", null);
    }

    // ── Refresh Token Tests ──────────────────────────────────────────────────

    /// <summary>
    /// Validates: Requirement 4.4
    /// Refresh response recalculates timezone offset (handles DST changes between sessions).
    /// </summary>
    [Fact]
    public async Task Refresh_RecalculatesTimezoneOffset()
    {
        // Arrange
        var db = CreateDb();
        var jwt = BuildJwtService();
        var timezoneResolver = Substitute.For<ITimezoneResolver>();
        // Simulate DST change: resolver now returns summer offset
        timezoneResolver.Resolve("US", "NY")
            .Returns(new TimezoneResolution("America/New_York", -240));

        var user = await SeedUser(db, countryCode: "US", stateCode: "NY");

        // Seed an active refresh token
        var rawRefresh = "test-refresh-token-raw";
        var tokenHash = Sha256Hex(rawRefresh);
        var refreshToken = RefreshToken.Create(user.Id, tokenHash, 7);
        db.RefreshTokens.Add(refreshToken);
        await db.SaveChangesAsync();

        // Re-load with navigation property
        jwt.HashToken(rawRefresh).Returns(tokenHash);

        var handler = new RefreshTokenCommandHandler(db, jwt, timezoneResolver, BuildConfig());

        // Act
        var result = await handler.Handle(
            new RefreshTokenCommand(rawRefresh), CancellationToken.None);

        // Assert
        result.TimezoneId.Should().Be("America/New_York");
        result.TimezoneOffsetMinutes.Should().Be(-240);
        timezoneResolver.Received(1).Resolve("US", "NY");
    }

    /// <summary>
    /// Validates: Requirement 4.4
    /// Refresh response includes timezone fields in the result.
    /// </summary>
    [Fact]
    public async Task Refresh_ResponseIncludesTimezoneFields()
    {
        // Arrange
        var db = CreateDb();
        var jwt = BuildJwtService();
        var timezoneResolver = Substitute.For<ITimezoneResolver>();
        timezoneResolver.Resolve("DE", null)
            .Returns(new TimezoneResolution("Europe/Berlin", 60));

        var user = await SeedUser(db, countryCode: "DE");

        var rawRefresh = "refresh-token-for-test";
        var tokenHash = Sha256Hex(rawRefresh);
        var refreshToken = RefreshToken.Create(user.Id, tokenHash, 7);
        db.RefreshTokens.Add(refreshToken);
        await db.SaveChangesAsync();

        jwt.HashToken(rawRefresh).Returns(tokenHash);

        var handler = new RefreshTokenCommandHandler(db, jwt, timezoneResolver, BuildConfig());

        // Act
        var result = await handler.Handle(
            new RefreshTokenCommand(rawRefresh), CancellationToken.None);

        // Assert
        result.TimezoneId.Should().Be("Europe/Berlin");
        result.TimezoneOffsetMinutes.Should().Be(60);
    }

    /// <summary>
    /// Validates: Requirement 4.4 (fallback behavior)
    /// Refresh with null country defaults to Asia/Jerusalem.
    /// </summary>
    [Fact]
    public async Task Refresh_NullCountry_DefaultsToAsiaJerusalem()
    {
        // Arrange
        var db = CreateDb();
        var jwt = BuildJwtService();
        var timezoneResolver = Substitute.For<ITimezoneResolver>();
        timezoneResolver.Resolve(null, null)
            .Returns(new TimezoneResolution("Asia/Jerusalem", 120));

        var user = await SeedUser(db); // no country/state

        var rawRefresh = "refresh-no-country";
        var tokenHash = Sha256Hex(rawRefresh);
        var refreshToken = RefreshToken.Create(user.Id, tokenHash, 7);
        db.RefreshTokens.Add(refreshToken);
        await db.SaveChangesAsync();

        jwt.HashToken(rawRefresh).Returns(tokenHash);

        var handler = new RefreshTokenCommandHandler(db, jwt, timezoneResolver, BuildConfig());

        // Act
        var result = await handler.Handle(
            new RefreshTokenCommand(rawRefresh), CancellationToken.None);

        // Assert
        result.TimezoneId.Should().Be("Asia/Jerusalem");
        result.TimezoneOffsetMinutes.Should().Be(120);
        timezoneResolver.Received(1).Resolve(null, null);
    }
}
