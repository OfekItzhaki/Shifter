using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Groups.Commands;

public record SetHomeLeavePriorityCommand(
    Guid SpaceId,
    Guid GroupId,
    Guid PersonId,
    decimal Priority) : IRequest<decimal>;

public class SetHomeLeavePriorityCommandHandler : IRequestHandler<SetHomeLeavePriorityCommand, decimal>
{
    private readonly AppDbContext _db;

    public SetHomeLeavePriorityCommandHandler(AppDbContext db) => _db = db;

    public async Task<decimal> Handle(SetHomeLeavePriorityCommand req, CancellationToken ct)
    {
        var membership = await _db.GroupMemberships
            .FirstOrDefaultAsync(m => m.GroupId == req.GroupId
                && m.PersonId == req.PersonId
                && m.SpaceId == req.SpaceId, ct)
            ?? throw new KeyNotFoundException("החבר לא נמצא בקבוצה.");

        membership.SetHomeLeavePriority(req.Priority);
        await _db.SaveChangesAsync(ct);

        return membership.HomeLeavePriority;
    }
}
