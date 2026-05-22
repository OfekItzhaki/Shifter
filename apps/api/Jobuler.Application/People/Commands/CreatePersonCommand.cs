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
    Guid RequestingUserId,
    string? PhoneNumber = null,
    string? Email = null) : IRequest<Guid>;

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

        // No duplicate name check at space level — same name is allowed in a space
        // (e.g., two "דניאל" in different groups). Duplicate check happens at group level
        // when adding a person to a group.

        // If no LinkedUserId, create as pending invitation
        var status = req.LinkedUserId.HasValue ? "accepted" : "pending";

        var person = Person.Create(req.SpaceId, req.FullName, req.DisplayName, req.LinkedUserId,
            phoneNumber: req.PhoneNumber, invitationStatus: status);
        if (!string.IsNullOrWhiteSpace(req.Email))
        {
            person.UpdateFull(person.FullName, person.DisplayName, person.ProfileImageUrl, person.PhoneNumber, person.Birthday, req.Email.Trim());
        }
        _db.People.Add(person);
        await _db.SaveChangesAsync(ct);
        return person.Id;
    }
}
