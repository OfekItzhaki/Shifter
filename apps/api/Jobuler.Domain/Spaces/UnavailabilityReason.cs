using Jobuler.Domain.Common;

namespace Jobuler.Domain.Spaces;

/// <summary>
/// A structured reason for marking a person as unavailable.
/// Scoped to a space — shared across all groups within that space.
/// Supports soft-delete via IsActive flag.
/// </summary>
public class UnavailabilityReason : AuditableEntity, ITenantScoped
{
    public Guid SpaceId { get; private set; }
    public string DisplayName { get; private set; } = default!;
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; } = true;

    private UnavailabilityReason() { }

    public static UnavailabilityReason Create(Guid spaceId, string displayName, int sortOrder)
    {
        ValidateDisplayName(displayName);

        return new UnavailabilityReason
        {
            SpaceId = spaceId,
            DisplayName = displayName.Trim(),
            SortOrder = sortOrder
        };
    }

    public void Update(string displayName, int sortOrder)
    {
        ValidateDisplayName(displayName);

        DisplayName = displayName.Trim();
        SortOrder = sortOrder;
        Touch();
    }

    public void Deactivate()
    {
        IsActive = false;
        Touch();
    }

    private static void ValidateDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new InvalidOperationException("Display name cannot be empty.");

        if (displayName.Trim().Length > 100)
            throw new InvalidOperationException("Display name cannot exceed 100 characters.");
    }
}
