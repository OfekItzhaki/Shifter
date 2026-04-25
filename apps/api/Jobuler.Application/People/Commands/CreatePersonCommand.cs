using Jobuler.Application.Common;
using Jobuler.Domain.People;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.People.Commands;

public record CreatePersonCommand(
    Guid SpaceId,
    string FullName,
    string? DisplayName,
    Guid? LinkedUserId,
    Guid RequestingUserId) : IRequest<Guid>;

public class CreatePersonCommandHandler : IRequestHandler<CreatePersonCommand, Guid>
{
    private readonly AppDbContext _db;
    private readonly IPermissionService _permissions;

    public CreatePersonCommandHandler(AppDbContext db, IPermissionService permissions)
    {
        _db = db;
        _permissions = permissions;
    }

    public async Task<Guid> Handle(CreatePersonCommand req, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(req.RequestingUserId, req.SpaceId, Permissions.PeopleManage, ct);

        // Duplicate name check (case-insensitive)
        var nameLower = req.FullName.Trim().ToLowerInvariant();
        var duplicate = await _db.People
            .AnyAsync(p => p.SpaceId == req.SpaceId && p.IsActive &&
                           p.FullName.ToLower() == nameLower, ct);
        if (duplicate)
            throw new ConflictException($"A person named '{req.FullName.Trim()}' already exists in this space.");

        // If no LinkedUserId, create as pending invitation
        var status = req.LinkedUserId.HasValue ? "accepted" : "pending";

        var person = Person.Create(req.SpaceId, req.FullName, req.DisplayName, req.LinkedUserId,
            phoneNumber: null, invitationStatus: status);
        _db.People.Add(person);
        await _db.SaveChangesAsync(ct);
        return person.Id;
    }
}
