using Jobuler.Application.Common;
using Jobuler.Domain.People;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace Jobuler.Application.People.Commands;

/// <summary>
/// Sends an invitation to a pending person via email or WhatsApp.
/// Stores the contact on the Person record and creates a PendingInvitation.
/// </summary>
public record InvitePersonCommand(
    Guid SpaceId,
    Guid PersonId,
    string Contact,
    string Channel,
    Guid RequestingUserId) : IRequest;

public class InvitePersonCommandHandler : IRequestHandler<InvitePersonCommand>
{
    private readonly AppDbContext _db;
    private readonly IPermissionService _permissions;
    private readonly IInvitationSender _invitationSender;

    public InvitePersonCommandHandler(
        AppDbContext db,
        IPermissionService permissions,
        IInvitationSender invitationSender)
    {
        _db = db;
        _permissions = permissions;
        _invitationSender = invitationSender;
    }

    public async Task Handle(InvitePersonCommand req, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(req.RequestingUserId, req.SpaceId, Permissions.PeopleManage, ct);

        var person = await _db.People
            .FirstOrDefaultAsync(p => p.Id == req.PersonId && p.SpaceId == req.SpaceId, ct)
            ?? throw new KeyNotFoundException("Person not found.");

        // Validate contact based on channel
        var channel = req.Channel.ToLowerInvariant();
        if (channel == "email")
        {
            if (!IsValidEmail(req.Contact))
                throw new InvalidOperationException("Invalid email address.");
        }
        else if (channel == "whatsapp")
        {
            if (!IsValidPhone(req.Contact))
                throw new InvalidOperationException("Invalid phone number.");
        }
        else
        {
            throw new InvalidOperationException("Channel must be 'email' or 'whatsapp'.");
        }

        // Create invitation token
        var (invitation, rawToken) = PendingInvitation.Create(
            req.SpaceId, req.PersonId, req.Contact, channel, req.RequestingUserId);

        _db.PendingInvitations.Add(invitation);

        // Store contact on person if not already set
        if (channel == "whatsapp" && string.IsNullOrWhiteSpace(person.PhoneNumber))
            person.SetPhoneNumber(req.Contact);

        await _db.SaveChangesAsync(ct);

        // Build invite URL — in production this comes from config
        var inviteUrl = $"https://jobuler.app/invitations/accept?token={rawToken}";

        // Send via the appropriate channel
        await _invitationSender.SendInvitationAsync(
            req.Contact, channel, inviteUrl, person.FullName, ct);
    }

    private static bool IsValidEmail(string email) =>
        Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");

    private static bool IsValidPhone(string phone) =>
        Regex.IsMatch(phone.Replace(" ", "").Replace("-", ""), @"^\+?[\d]{7,15}$");
}
