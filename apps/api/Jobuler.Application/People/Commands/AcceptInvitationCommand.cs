using Jobuler.Domain.People;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.People.Commands;

/// <summary>
/// Accepts a pending invitation by token.
/// Links the Person to the authenticated User and sets InvitationStatus = "accepted".
/// </summary>
public record AcceptInvitationCommand(
    string Token,
    Guid UserId) : IRequest<AcceptInvitationResult>;

public record AcceptInvitationResult(Guid PersonId, Guid SpaceId);

public class AcceptInvitationCommandHandler : IRequestHandler<AcceptInvitationCommand, AcceptInvitationResult>
{
    private readonly AppDbContext _db;

    public AcceptInvitationCommandHandler(AppDbContext db) => _db = db;

    public async Task<AcceptInvitationResult> Handle(AcceptInvitationCommand req, CancellationToken ct)
    {
        var tokenHash = PendingInvitation.HashToken(req.Token);

        var invitation = await _db.PendingInvitations
            .FirstOrDefaultAsync(i => i.TokenHash == tokenHash, ct)
            ?? throw new KeyNotFoundException("Invalid or expired invitation token.");

        if (invitation.IsAccepted)
            throw new InvalidOperationException("This invitation has already been accepted.");

        if (invitation.IsExpired)
            throw new InvalidOperationException("This invitation has expired. Please ask the admin to resend it.");

        var person = await _db.People
            .FirstOrDefaultAsync(p => p.Id == invitation.PersonId && p.SpaceId == invitation.SpaceId, ct)
            ?? throw new KeyNotFoundException("Person not found.");

        // Link user to person
        person.LinkUser(req.UserId);
        invitation.Accept();

        await _db.SaveChangesAsync(ct);
        return new AcceptInvitationResult(person.Id, person.SpaceId);
    }
}
