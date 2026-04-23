using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Groups.Commands;

public record UpdateGroupSettingsCommand(
    Guid SpaceId,
    Guid GroupId,
    int SolverHorizonDays) : IRequest;

public class UpdateGroupSettingsCommandHandler : IRequestHandler<UpdateGroupSettingsCommand>
{
    private readonly AppDbContext _db;
    public UpdateGroupSettingsCommandHandler(AppDbContext db) => _db = db;

    public async Task Handle(UpdateGroupSettingsCommand req, CancellationToken ct)
    {
        var group = await _db.Groups
            .FirstOrDefaultAsync(g => g.Id == req.GroupId && g.SpaceId == req.SpaceId, ct);
        if (group is null) throw new KeyNotFoundException("Group not found.");

        group.UpdateSettings(req.SolverHorizonDays);
        await _db.SaveChangesAsync(ct);
    }
}
