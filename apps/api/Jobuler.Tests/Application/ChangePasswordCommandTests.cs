using FluentAssertions;
using Jobuler.Application.Auth.Commands;
using Jobuler.Domain.Identity;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Jobuler.Tests.Application;

public class ChangePasswordCommandTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task Handle_WithCorrectCurrentPassword_UpdatesPasswordAndRevokesRefreshTokens()
    {
        await using var db = CreateDb();
        var user = User.Create(
            "user@example.com",
            "Test User",
            BCrypt.Net.BCrypt.HashPassword("OldPass1!", workFactor: 4));
        var firstToken = RefreshToken.Create(user.Id, "first-token-hash", expiryDays: 7);
        var secondToken = RefreshToken.Create(user.Id, "second-token-hash", expiryDays: 7);
        db.Users.Add(user);
        db.RefreshTokens.AddRange(firstToken, secondToken);
        await db.SaveChangesAsync();

        await new ChangePasswordCommandHandler(db)
            .Handle(new ChangePasswordCommand(user.Id, "OldPass1!", "NewPass1!"), CancellationToken.None);

        var reloaded = await db.Users.SingleAsync(u => u.Id == user.Id);
        BCrypt.Net.BCrypt.Verify("NewPass1!", reloaded.PasswordHash).Should().BeTrue();
        BCrypt.Net.BCrypt.Verify("OldPass1!", reloaded.PasswordHash).Should().BeFalse();

        var tokens = await db.RefreshTokens.Where(t => t.UserId == user.Id).ToListAsync();
        tokens.Should().OnlyContain(t => t.RevokedAt.HasValue);
    }

    [Fact]
    public async Task Handle_WithWrongCurrentPassword_DoesNotChangePasswordOrRevokeTokens()
    {
        await using var db = CreateDb();
        var originalHash = BCrypt.Net.BCrypt.HashPassword("OldPass1!", workFactor: 4);
        var user = User.Create("user@example.com", "Test User", originalHash);
        var refreshToken = RefreshToken.Create(user.Id, "token-hash", expiryDays: 7);
        db.Users.Add(user);
        db.RefreshTokens.Add(refreshToken);
        await db.SaveChangesAsync();

        var act = () => new ChangePasswordCommandHandler(db)
            .Handle(new ChangePasswordCommand(user.Id, "WrongPass1!", "NewPass1!"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();

        var reloaded = await db.Users.SingleAsync(u => u.Id == user.Id);
        reloaded.PasswordHash.Should().Be(originalHash);
        (await db.RefreshTokens.SingleAsync(t => t.UserId == user.Id)).RevokedAt.Should().BeNull();
    }
}
