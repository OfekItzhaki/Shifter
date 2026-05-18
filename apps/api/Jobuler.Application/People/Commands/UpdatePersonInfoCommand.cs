using Jobuler.Application.Common;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.People.Commands;

/// <summary>
/// Updates a person's profile information.
/// Admins can update any non-admin person's info.
/// Users can update their own linked person's info.
/// </summary>
public record UpdatePersonInfoCommand(
    Guid SpaceId,
    Guid PersonId,
    Guid RequestingUserId,
    string FullName,
    string? DisplayName,
    string? PhoneNumber,
    string? ProfileImageUrl,
    DateOnly? Birthday,
    string? Email = null) : IRequest;

public class UpdatePersonInfoCommandHandler : IRequestHandler<UpdatePersonInfoCommand>
{
    private readonly AppDbContext _db;
    private readonly IPermissionService _permissions;

    public UpdatePersonInfoCommandHandler(AppDbContext db, IPermissionService permissions)
    {
        _db = db;
        _permissions = permissions;
    }

    public async Task Handle(UpdatePersonInfoCommand req, CancellationToken ct)
    {
        var person = await _db.People
            .FirstOrDefaultAsync(p => p.Id == req.PersonId && p.SpaceId == req.SpaceId, ct)
            ?? throw new KeyNotFoundException("Person not found.");

        // Allow if: caller is admin (people.manage) OR caller is the linked user
        var isAdmin = await _permissions.HasPermissionAsync(req.RequestingUserId, req.SpaceId, Permissions.PeopleManage, ct);
        var isOwn = person.LinkedUserId == req.RequestingUserId;

        if (!isAdmin && !isOwn)
            throw new UnauthorizedAccessException("You can only edit your own profile.");

        person.UpdateFull(req.FullName, req.DisplayName, req.ProfileImageUrl, req.PhoneNumber, req.Birthday, req.Email);
        await _db.SaveChangesAsync(ct);
    }
}
