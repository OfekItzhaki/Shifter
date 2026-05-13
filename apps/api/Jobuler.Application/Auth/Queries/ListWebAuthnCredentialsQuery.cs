using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Auth.Queries;

/// <summary>
/// Lists all WebAuthn credentials for the authenticated user.
/// </summary>
public record ListWebAuthnCredentialsQuery(Guid UserId) : IRequest<List<WebAuthnCredentialDto>>;

public record WebAuthnCredentialDto(
    Guid Id,
    string? Nickname,
    DateTime CreatedAt,
    DateTime? LastUsedAt,
    bool IsDisabled);

public class ListWebAuthnCredentialsQueryHandler
    : IRequestHandler<ListWebAuthnCredentialsQuery, List<WebAuthnCredentialDto>>
{
    private readonly AppDbContext _db;

    public ListWebAuthnCredentialsQueryHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<WebAuthnCredentialDto>> Handle(
        ListWebAuthnCredentialsQuery request, CancellationToken ct)
    {
        return await _db.WebAuthnCredentials
            .Where(c => c.UserId == request.UserId)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new WebAuthnCredentialDto(
                c.Id,
                c.Nickname,
                c.CreatedAt,
                c.LastUsedAt,
                c.IsDisabled))
            .ToListAsync(ct);
    }
}
