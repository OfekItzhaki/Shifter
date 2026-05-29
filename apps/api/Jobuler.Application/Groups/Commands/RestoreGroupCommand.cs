using Jobuler.Application.Common;
using Jobuler.Domain.Notifications;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Groups.Commands;

public record RestoreGroupCommand(Guid SpaceId, Guid GroupId, Guid RequestingUserId) : IRequest;

public class RestoreGroupCommandHandler : IRequestHandler<RestoreGroupCommand>
{
    private readonly AppDbContext _db;
    private readonly IEmailSender _email;
    public RestoreGroupCommandHandler(AppDbContext db, IEmailSender email) { _db = db; _email = email; }

    public async Task Handle(RestoreGroupCommand req, CancellationToken ct)
    {
        // Load including soft-deleted (no DeletedAt filter)
        var group = await _db.Groups
            .FirstOrDefaultAsync(g => g.Id == req.GroupId && g.SpaceId == req.SpaceId, ct)
            ?? throw new KeyNotFoundException("Group not found.");

        var ownerMembership = await _db.GroupMemberships
            .Join(_db.People, m => m.PersonId, p => p.Id, (m, p) => new { m, p })
            .FirstOrDefaultAsync(x => x.m.GroupId == req.GroupId && x.m.IsOwner && x.p.LinkedUserId == req.RequestingUserId, ct);
        if (ownerMembership is null)
            throw new UnauthorizedAccessException("Only the group owner can restore the group.");

        if (group.DeletedAt is null)
            throw new InvalidOperationException("Group is not deleted.");
        if (group.DeletedAt < DateTime.UtcNow.AddDays(-30))
            throw new InvalidOperationException("Recovery window has expired (30 days).");

        group.Restore();

        // Notify all members with linked users
        var membersWithUsers = await _db.GroupMemberships
            .Where(m => m.GroupId == req.GroupId)
            .Join(_db.People, m => m.PersonId, p => p.Id, (m, p) => p)
            .Where(p => p.LinkedUserId.HasValue)
            .Join(_db.Users, p => p.LinkedUserId, u => u.Id, (p, u) => new { u.Id, u.Email })
            .ToListAsync(ct);

        foreach (var member in membersWithUsers)
        {
            _db.Notifications.Add(Notification.Create(
                req.SpaceId, member.Id,
                "group_restored",
                $"Group {group.Name} restored",
                $"The group \"{group.Name}\" was restored by the owner.",
                System.Text.Json.JsonSerializer.Serialize(new { groupId = req.GroupId })));

            await _email.SendAsync(
                member.Email,
                $"Group {group.Name} restored",
                $"<p>The group <strong>{group.Name}</strong> has been restored. You are a member again.</p>",
                ct);
        }

        await _db.SaveChangesAsync(ct);
    }
}
