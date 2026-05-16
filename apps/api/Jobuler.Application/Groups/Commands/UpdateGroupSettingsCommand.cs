using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Groups.Commands;

public record UpdateGroupSettingsCommand(
    Guid SpaceId,
    Guid GroupId,
    int SolverHorizonDays,
    DateTime? SolverStartDateTime = null,
    bool? AutoPublish = null,
    int? MinRestBetweenShiftsHours = null) : IRequest;

public class UpdateGroupSettingsCommandHandler : IRequestHandler<UpdateGroupSettingsCommand>
{
    private readonly AppDbContext _db;
    public UpdateGroupSettingsCommandHandler(AppDbContext db) => _db = db;

    public async Task Handle(UpdateGroupSettingsCommand req, CancellationToken ct)
    {
        var group = await _db.Groups
            .FirstOrDefaultAsync(g => g.Id == req.GroupId && g.SpaceId == req.SpaceId, ct);
        if (group is null) throw new KeyNotFoundException("Group not found.");

        // Validate: SolverStartDateTime cannot be in the past
        if (req.SolverStartDateTime.HasValue)
        {
            var startTime = DateTime.SpecifyKind(req.SolverStartDateTime.Value, DateTimeKind.Utc);
            if (startTime < DateTime.UtcNow)
            {
                throw new InvalidOperationException("Solver start date cannot be in the past.");
            }
        }

        group.UpdateSettings(req.SolverHorizonDays, req.SolverStartDateTime);
        if (req.AutoPublish.HasValue)
            group.SetAutoPublish(req.AutoPublish.Value);
        if (req.MinRestBetweenShiftsHours.HasValue)
            group.SetMinRestBetweenShifts(req.MinRestBetweenShiftsHours.Value);
        await _db.SaveChangesAsync(ct);
    }
}
