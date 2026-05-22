using Jobuler.Domain.Common;

namespace Jobuler.Domain.Spaces;

public class Space : AuditableEntity
{
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public bool IsActive { get; private set; } = true;
    public string Locale { get; private set; } = "he";
    public string? InviteCode { get; private set; }

    private Space() { }

    public static Space Create(string name, Guid ownerUserId, string? description = null, string locale = "he")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new Space
        {
            Name = name.Trim(),
            Description = description?.Trim(),
            OwnerUserId = ownerUserId,
            Locale = locale,
            InviteCode = GenerateInviteCode()
        };
    }

    public void Update(string name, string? description, string locale)
    {
        Name = name.Trim();
        Description = description?.Trim();
        Locale = locale;
        Touch();
    }

    public void TransferOwnership(Guid newOwnerUserId)
    {
        OwnerUserId = newOwnerUserId;
        Touch();
    }

    public string RegenerateInviteCode()
    {
        InviteCode = GenerateInviteCode();
        Touch();
        return InviteCode;
    }

    public void Deactivate() { IsActive = false; Touch(); }

    private static string GenerateInviteCode() =>
        Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
}
