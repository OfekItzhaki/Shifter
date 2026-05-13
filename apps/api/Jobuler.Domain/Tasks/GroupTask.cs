using Jobuler.Domain.Common;

namespace Jobuler.Domain.Tasks;

/// <summary>
/// Flat, group-scoped task entity.
///
/// A task defines a recurring need (e.g. "Kitchen") with a window (StartsAt → EndsAt).
/// The solver fills that window with shifts of ShiftDurationMinutes length.
/// RequiredHeadcount = how many people per shift.
/// AllowsDoubleShift = a person can do two consecutive shifts.
/// AllowsOverlap = a person can be assigned to this task while assigned to another.
/// </summary>
public class GroupTask : AuditableEntity, ITenantScoped
{
    public Guid SpaceId { get; private set; }
    public Guid GroupId { get; private set; }
    public string Name { get; private set; } = default!;
    public DateTime StartsAt { get; private set; }
    public DateTime EndsAt { get; private set; }
    /// <summary>Duration of each shift in minutes (e.g. 240 = 4h, 30 = 30min).</summary>
    public int ShiftDurationMinutes { get; private set; } = 240;
    public int RequiredHeadcount { get; private set; } = 1;
    public TaskBurdenLevel BurdenLevel { get; private set; } = TaskBurdenLevel.Normal;
    public bool AllowsDoubleShift { get; private set; } = false;
    public bool AllowsOverlap { get; private set; } = false;
    /// <summary>
    /// Optional daily start time. When set, the solver only generates shifts
    /// within the [DailyStartTime, DailyEndTime] window each day.
    /// Null means 24/7 (no daily restriction).
    /// </summary>
    public TimeOnly? DailyStartTime { get; private set; }
    /// <summary>Optional daily end time. Must be set together with DailyStartTime.</summary>
    public TimeOnly? DailyEndTime { get; private set; }

    /// <summary>
    /// Structured qualification requirements per shift.
    /// Each entry specifies how many seats require a given qualification and whether it is mandatory.
    /// </summary>
    public List<QualificationRequirement> QualificationRequirements { get; private set; } = new();

    /// <summary>
    /// Backward-compat derived property: names of qualifications that are mandatory (hard requirements).
    /// Derived from QualificationRequirements where Mandatory == true.
    /// </summary>
    public List<string> RequiredQualificationNames =>
        QualificationRequirements.Where(r => r.Mandatory).Select(r => r.QualificationName).ToList();

    public bool IsActive { get; private set; } = true;
    public Guid? CreatedByUserId { get; private set; }
    public Guid? UpdatedByUserId { get; private set; }

    private GroupTask() { }

    public static GroupTask Create(
        Guid spaceId,
        Guid groupId,
        string name,
        DateTime startsAt,
        DateTime endsAt,
        int shiftDurationMinutes,
        int requiredHeadcount,
        TaskBurdenLevel burdenLevel,
        bool allowsDoubleShift,
        bool allowsOverlap,
        Guid createdByUserId,
        TimeOnly? dailyStartTime = null,
        TimeOnly? dailyEndTime = null,
        List<QualificationRequirement>? qualificationRequirements = null) =>
        new()
        {
            SpaceId = spaceId,
            GroupId = groupId,
            Name = name.Trim(),
            StartsAt = startsAt,
            EndsAt = endsAt,
            ShiftDurationMinutes = shiftDurationMinutes,
            RequiredHeadcount = requiredHeadcount,
            BurdenLevel = burdenLevel,
            AllowsDoubleShift = allowsDoubleShift,
            AllowsOverlap = allowsOverlap,
            DailyStartTime = dailyStartTime,
            DailyEndTime = dailyEndTime,
            QualificationRequirements = qualificationRequirements ?? new(),
            CreatedByUserId = createdByUserId
        };

    public void Update(
        string name,
        DateTime startsAt,
        DateTime endsAt,
        int shiftDurationMinutes,
        int requiredHeadcount,
        TaskBurdenLevel burdenLevel,
        bool allowsDoubleShift,
        bool allowsOverlap,
        Guid updatedByUserId,
        TimeOnly? dailyStartTime = null,
        TimeOnly? dailyEndTime = null,
        List<QualificationRequirement>? qualificationRequirements = null)
    {
        Name = name.Trim();
        StartsAt = startsAt;
        EndsAt = endsAt;
        ShiftDurationMinutes = shiftDurationMinutes;
        RequiredHeadcount = requiredHeadcount;
        BurdenLevel = burdenLevel;
        AllowsDoubleShift = allowsDoubleShift;
        AllowsOverlap = allowsOverlap;
        DailyStartTime = dailyStartTime;
        DailyEndTime = dailyEndTime;
        QualificationRequirements = qualificationRequirements ?? new();
        UpdatedByUserId = updatedByUserId;
        Touch();
    }

    public void Deactivate(Guid updatedByUserId)
    {
        IsActive = false;
        UpdatedByUserId = updatedByUserId;
        Touch();
    }
}
