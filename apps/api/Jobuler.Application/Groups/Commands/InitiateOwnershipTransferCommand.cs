using Jobuler.Application.Common;
using Jobuler.Domain.Groups;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Groups.Commands;

public record InitiateOwnershipTransferCommand(
    Guid SpaceId, Guid GroupId, Guid CurrentOwnerUserId, Guid ProposedPersonId) : IRequest;

public class InitiateOwnershipTransferCommandHandler : IRequestHandler<InitiateOwnershipTransferCommand>
{
    private readonly AppDbContext _db;
    private readonly IEmailSender _email;
    public InitiateOwnershipTransferCommandHandler(AppDbContext db, IEmailSender email) { _db = db; _email = email; }

    public async Task Handle(InitiateOwnershipTransferCommand req, CancellationToken ct)
    {
        // Verify caller is owner
        var ownerPerson = await _db.People
            .FirstOrDefaultAsync(p => p.SpaceId == req.SpaceId && p.LinkedUserId == req.CurrentOwnerUserId, ct)
            ?? throw new UnauthorizedAccessException("Caller is not a member of this space.");

        var ownerMembership = await _db.GroupMemberships
            .FirstOrDefaultAsync(m => m.GroupId == req.GroupId && m.PersonId == ownerPerson.Id && m.IsOwner, ct)
            ?? throw new UnauthorizedAccessException("Only the group owner can initiate an ownership transfer.");

        // Verify proposed person is a member
        var proposedMembership = await _db.GroupMemberships
            .FirstOrDefaultAsync(m => m.GroupId == req.GroupId && m.PersonId == req.ProposedPersonId, ct)
            ?? throw new InvalidOperationException("Proposed new owner is not a member of this group.");

        // Check no pending transfer exists
        var existing = await _db.PendingOwnershipTransfers
            .AnyAsync(t => t.GroupId == req.GroupId, ct);
        if (existing)
            throw new ConflictException("A pending ownership transfer already exists for this group.");

        var transfer = PendingOwnershipTransfer.Create(
            req.SpaceId, req.GroupId, ownerPerson.Id, req.ProposedPersonId);
        _db.PendingOwnershipTransfers.Add(transfer);
        await _db.SaveChangesAsync(ct);

        // Send confirmation email to proposed new owner
        var proposedUser = await _db.People
            .Where(p => p.Id == req.ProposedPersonId && p.LinkedUserId.HasValue)
            .Join(_db.Users, p => p.LinkedUserId, u => u.Id, (p, u) => u)
            .FirstOrDefaultAsync(ct);

        if (proposedUser is not null)
        {
            var group = await _db.Groups.AsNoTracking().FirstOrDefaultAsync(g => g.Id == req.GroupId, ct);
            var confirmUrl = $"http://localhost:3000/groups/confirm-transfer?token={transfer.ConfirmationToken}";
            await _email.SendAsync(
                proposedUser.Email,
                $"Group ownership transfer: {group?.Name}",
                $"<p>You have been invited to take ownership of the group <strong>{group?.Name}</strong>.</p><p><a href='{confirmUrl}'>Click here to confirm</a></p>",
                ct);
        }
    }
}
