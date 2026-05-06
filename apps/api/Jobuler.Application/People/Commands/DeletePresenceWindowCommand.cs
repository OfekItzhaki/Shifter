using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.People.Commands;

public record DeletePresenceWindowCommand(
    Guid SpaceId, Guid PersonId, Guid WindowId) : IRequest;

public class DeletePresenceWindowCommandHandler : IRequestHandler<DeletePresenceWindowCommand>
{
    private readonly AppDbContext _db;
    public DeletePresenceWindowCommandHandler(AppDbContext db) => _db = db;

    public async Task Handle(DeletePresenceWindowCommand req, CancellationToken ct)
    {
        var window = await _db.PresenceWindows
            .FirstOrDefaultAsync(w =>
                w.Id == req.WindowId &&
                w.PersonId == req.PersonId &&
                w.SpaceId == req.SpaceId &&
                !w.IsDerived, ct)
            ?? throw new KeyNotFoundException("Presence window not found.");

        _db.PresenceWindows.Remove(window);
        await _db.SaveChangesAsync(ct);
    }
}
