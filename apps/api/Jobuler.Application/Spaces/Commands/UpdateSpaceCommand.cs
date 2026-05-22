using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Spaces.Commands;

public record UpdateSpaceCommand(
    Guid SpaceId,
    string Name,
    string? Description,
    string Locale,
    Guid RequestingUserId) : IRequest;

public class UpdateSpaceCommandHandler : IRequestHandler<UpdateSpaceCommand>
{
    private readonly AppDbContext _db;

    public UpdateSpaceCommandHandler(AppDbContext db) => _db = db;

    public async Task Handle(UpdateSpaceCommand request, CancellationToken ct)
    {
        var space = await _db.Spaces.FirstOrDefaultAsync(s => s.Id == request.SpaceId, ct)
            ?? throw new KeyNotFoundException("Space not found.");

        if (space.OwnerUserId != request.RequestingUserId)
            throw new UnauthorizedAccessException("Only the space owner can update settings.");

        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length < 2 || name.Length > 100)
            throw new InvalidOperationException("Space name must be between 2 and 100 characters.");

        var description = request.Description?.Trim();
        if (description is not null && description.Length > 500)
            throw new InvalidOperationException("Description must not exceed 500 characters.");

        space.Update(name, description, request.Locale);
        await _db.SaveChangesAsync(ct);
    }
}
