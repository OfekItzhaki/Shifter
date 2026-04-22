using Jobuler.Application.Auth;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Auth.Commands;

public record RevokeTokenCommand(string RefreshToken) : IRequest;

public class RevokeTokenCommandHandler : IRequestHandler<RevokeTokenCommand>
{
    private readonly AppDbContext _db;
    private readonly IJwtService _jwt;

    public RevokeTokenCommandHandler(AppDbContext db, IJwtService jwt)
    {
        _db = db;
        _jwt = jwt;
    }

    public async Task Handle(RevokeTokenCommand request, CancellationToken ct)
    {
        var hash = _jwt.HashToken(request.RefreshToken);
        var token = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (token is not null && token.IsActive)
        {
            token.Revoke();
            await _db.SaveChangesAsync(ct);
        }
    }
}
