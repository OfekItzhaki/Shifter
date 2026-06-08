using FluentValidation;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Auth.Commands;

public record ChangePasswordCommand(
    Guid UserId,
    string CurrentPassword,
    string NewPassword) : IRequest;

public class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword)
            .MinimumLength(8)
            .WithMessage("Password must be at least 8 characters.");
    }
}

public class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand>
{
    private readonly AppDbContext _db;

    public ChangePasswordCommandHandler(AppDbContext db) => _db = db;

    public async Task Handle(ChangePasswordCommand req, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == req.UserId, ct)
            ?? throw new KeyNotFoundException("User not found.");

        if (!BCrypt.Net.BCrypt.Verify(req.CurrentPassword, user.PasswordHash))
            throw new UnauthorizedAccessException("Current password is incorrect.");

        user.SetPasswordHash(BCrypt.Net.BCrypt.HashPassword(req.NewPassword, workFactor: 12));

        var refreshTokens = await _db.RefreshTokens
            .Where(rt => rt.UserId == user.Id && rt.RevokedAt == null)
            .ToListAsync(ct);

        foreach (var token in refreshTokens)
            token.Revoke();

        await _db.SaveChangesAsync(ct);
    }
}
