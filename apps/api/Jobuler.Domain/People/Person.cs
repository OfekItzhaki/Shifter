using Jobuler.Domain.Common;

namespace Jobuler.Domain.People;

public class Person : AuditableEntity, ITenantScoped
{
    public Guid SpaceId { get; private set; }
    public Guid? LinkedUserId { get; private set; }   // optional link to auth user
    public string FullName { get; private set; } = default!;
    public string? DisplayName { get; private set; }
    public string? ProfileImageUrl { get; private set; }
    public bool IsActive { get; private set; } = true;
    public string? PhoneNumber { get; private set; }
    public string? InvitationStatus { get; private set; } = "accepted"; // "pending" | "accepted"

    private Person() { }

    public static Person Create(Guid spaceId, string fullName, string? displayName = null, Guid? linkedUserId = null, string? phoneNumber = null, string invitationStatus = "accepted")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        return new Person
        {
            SpaceId = spaceId,
            FullName = fullName.Trim(),
            DisplayName = displayName?.Trim(),
            LinkedUserId = linkedUserId,
            PhoneNumber = phoneNumber?.Trim(),
            InvitationStatus = invitationStatus
        };
    }

    public void SetInvitationStatus(string status) { InvitationStatus = status; Touch(); }
    public void LinkUser(Guid userId) { LinkedUserId = userId; InvitationStatus = "accepted"; Touch(); }
    public void SetPhoneNumber(string phone) { PhoneNumber = phone?.Trim(); Touch(); }

    public void Update(string fullName, string? displayName, string? profileImageUrl)
    {
        FullName = fullName.Trim();
        DisplayName = displayName?.Trim();
        ProfileImageUrl = profileImageUrl;
        Touch();
    }

    public void Deactivate() { IsActive = false; Touch(); }
    public void Activate()   { IsActive = true;  Touch(); }
}
