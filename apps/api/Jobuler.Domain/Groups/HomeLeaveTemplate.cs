using Jobuler.Domain.Common;

namespace Jobuler.Domain.Groups;

/// <summary>
/// Reusable home-leave configuration template scoped to a space.
/// Allows admins to save and load standard leave configurations.
/// </summary>
public class HomeLeaveTemplate : Entity, ITenantScoped
{
    public Guid SpaceId { get; private set; }
    public string Name { get; private set; } = default!;
    public decimal MinRestHours { get; private set; }
    public decimal EligibilityThresholdHours { get; private set; }
    public int LeaveCapacity { get; private set; }
    public decimal LeaveDurationHours { get; private set; }

    private HomeLeaveTemplate() { }

    public static HomeLeaveTemplate Create(
        Guid spaceId,
        string name,
        decimal minRestHours,
        decimal eligibilityThresholdHours,
        int leaveCapacity,
        decimal leaveDurationHours)
    {
        ValidateName(name);

        return new()
        {
            SpaceId = spaceId,
            Name = name,
            MinRestHours = minRestHours,
            EligibilityThresholdHours = eligibilityThresholdHours,
            LeaveCapacity = leaveCapacity,
            LeaveDurationHours = leaveDurationHours
        };
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("שם התבנית חייב להיות בין 1 ל-100 תווים.");

        if (name.Length > 100)
            throw new InvalidOperationException("שם התבנית חייב להיות בין 1 ל-100 תווים.");

        if (name != name.Trim())
            throw new InvalidOperationException("שם התבנית לא יכול להכיל רווחים בהתחלה או בסוף.");
    }
}
