using Jobuler.Domain.Groups;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.People.Commands;

public record AssignRoleToPersonCommand(
    Guid SpaceId, Guid PersonId, Guid RoleId) : IRequest;

public class AssignRoleToPersonCommandHandler : IRequestHandler<AssignRoleToPersonCommand>
{
    private readonly AppDbContext _db;
    public AssignRoleToPersonCommandHandler(AppDbContext db) => _db = db;

    public async Task Handle(AssignRoleToPersonCommand req, CancellationToken ct)
    {
        // Verify person and role belong to this space
        var personExists = await _db.People.AnyAsync(
            p => p.Id == req.PersonId && p.SpaceId == req.SpaceId, ct);
        if (!personExists) throw new KeyNotFoundException("Person not found.");

        var roleExists = await _db.SpaceRoles.AnyAsync(
            r => r.Id == req.RoleId && r.SpaceId == req.SpaceId, ct);
        if (!roleExists) throw new KeyNotFoundException("Role not found.");

        var alreadyAssigned = await _db.PersonRoleAssignments.AnyAsync(
            a => a.PersonId == req.PersonId && a.RoleId == req.RoleId, ct);
        if (alreadyAssigned) return;

        _db.PersonRoleAssignments.Add(
            PersonRoleAssignment.Create(req.SpaceId, req.PersonId, req.RoleId));
        await _db.SaveChangesAsync(ct);
    }
}

public record RemoveRoleFromPersonCommand(
    Guid SpaceId, Guid PersonId, Guid RoleId) : IRequest;

public class RemoveRoleFromPersonCommandHandler : IRequestHandler<RemoveRoleFromPersonCommand>
{
    private readonly AppDbContext _db;
    public RemoveRoleFromPersonCommandHandler(AppDbContext db) => _db = db;

    public async Task Handle(RemoveRoleFromPersonCommand req, CancellationToken ct)
    {
        var assignment = await _db.PersonRoleAssignments.FirstOrDefaultAsync(
            a => a.PersonId == req.PersonId && a.RoleId == req.RoleId
              && a.SpaceId == req.SpaceId, ct);
        if (assignment is null) return;

        _db.PersonRoleAssignments.Remove(assignment);
        await _db.SaveChangesAsync(ct);
    }
}
