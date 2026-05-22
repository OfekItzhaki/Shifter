using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Spaces.Commands;

public record RegenerateSpaceInviteCodeCommand(Guid SpaceId, Guid RequestingUserId) : IRequest<string>;

public class RegenerateSpaceInviteCodeCommandHandler : IRequestHandler<RegenerateSpaceInviteCodeCommand, string>
{
    private readonly AppDbContext _db;

    public RegenerateSpaceInviteCodeCommandHandler(AppDbContext db) => _db = db;

    public async Task<string> Handle(RegenerateSpaceInviteCodeCommand request, CancellationToken ct)
    {
        var space = await _db.Spaces.FirstOrDefaultAsync(s => s.Id == request.SpaceId, ct)
            ?? throw new KeyNotFoundException("Space not found.");

        if (space.OwnerUserId != request.RequestingUserId)
            throw new UnauthorizedAccessException("Only the space owner can regenerate the invite code.");

        var newCode = space.RegenerateInviteCode();
        await _db.SaveChangesAsync(ct);

        return newCode;
    }
}
