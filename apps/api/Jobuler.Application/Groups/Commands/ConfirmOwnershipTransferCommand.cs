using Jobuler.Domain.Notifications;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Groups.Commands;

public record ConfirmOwnershipTransferCommand(string ConfirmationToken) : IRequest;

public class ConfirmOwnershipTransferCommandHandler : IRequestHandler<ConfirmOwnershipTransferCommand>
{
    private readonly AppDbContext _db;
    public ConfirmOwnershipTransferCommandHandler(AppDbContext db) => _db = db;

    public async Task Handle(ConfirmOwnershipTransferCommand req, CancellationToken ct)
    {
        var transfer = await _db.PendingOwnershipTransfers
            .FirstOrDefaultAsync(t => t.ConfirmationToken == req.ConfirmationToken, ct)
            ?? throw new InvalidOperationException("Invalid or expired confirmation token.");

        if (transfer.IsExpired)
            throw new InvalidOperationException("This confirmation link has expired.");

        // Swap ownership atomically
        var currentOwnerMembership = await _db.GroupMemberships
            .FirstOrDefaultAsync(m => m.GroupId == transfer.GroupId && m.PersonId == transfer.CurrentOwnerPersonId, ct)
            ?? throw new InvalidOperationException("Current owner membership not found.");

        var newOwnerMembership = await _db.GroupMemberships
            .FirstOrDefaultAsync(m => m.GroupId == transfer.GroupId && m.PersonId == transfer.ProposedOwnerPersonId, ct)
            ?? throw new InvalidOperationException("Proposed owner membership not found.");

        currentOwnerMembership.SetOwner(false);
        newOwnerMembership.SetOwner(true);
        _db.PendingOwnershipTransfers.Remove(transfer);

        // Notify the new owner
        var newOwnerPerson = await _db.People.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == transfer.ProposedOwnerPersonId, ct);
        if (newOwnerPerson?.LinkedUserId is not null)
        {
            var group = await _db.Groups.AsNoTracking()
                .FirstOrDefaultAsync(g => g.Id == transfer.GroupId, ct);
            _db.Notifications.Add(Notification.Create(
                transfer.SpaceId, newOwnerPerson.LinkedUserId.Value,
                "group.ownership_transferred",
                "הבעלות על הקבוצה הועברה אליך",
                $"אתה כעת הבעלים של הקבוצה \"{group?.Name ?? "הקבוצה"}\".",
                System.Text.Json.JsonSerializer.Serialize(new { groupId = transfer.GroupId })));
        }

        await _db.SaveChangesAsync(ct);
    }
}
