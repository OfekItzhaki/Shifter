using System.Security.Cryptography;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Auth.Commands;

public class VerifyEmailCommandHandler : IRequestHandler<VerifyEmailCommand>
{
    private readonly AppDbContext _db;

    public VerifyEmailCommandHandler(AppDbContext db) => _db = db;

    public async Task Handle(VerifyEmailCommand request, CancellationToken ct)
    {
        var tokenHash = ComputeSha256(request.Token.Trim().ToLowerInvariant());

        var verificationToken = await _db.EmailVerificationTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct)
            ?? throw new KeyNotFoundException("Invalid or expired verification link.");

        if (!verificationToken.IsValid)
            throw new InvalidOperationException("This verification link has expired or has already been used. Please request a new one.");

        verificationToken.MarkUsed();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == verificationToken.UserId, ct)
            ?? throw new KeyNotFoundException("User not found.");

        user.MarkEmailVerified();

        await _db.SaveChangesAsync(ct);
    }

    private static string ComputeSha256(string input)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
