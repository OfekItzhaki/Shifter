using Jobuler.Domain.Common;

namespace Jobuler.Domain.Groups;

public class Group : AuditableEntity, ITenantScoped
{
    public Guid SpaceId { get; private set; }
    public Guid? GroupTypeId { get; private set; }
    public Guid? CreatedByUserId { get; private set; }
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; } = true;
    public int SolverHorizonDays { get; private set; } = 7;
    public DateTime? SolverStartDateTime { get; private set; }
    public bool AutoPublish { get; private set; } = false;
    public bool IsClosedBase { get; private set; } = false;
    public int MinRestBetweenShiftsHours { get; private set; } = 8;
    public string? JoinCode { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    private Group() { }

    public static Group Create(Guid spaceId, Guid? groupTypeId, string name, string? description = null, Guid? createdByUserId = null) =>
        new()
        {
            SpaceId = spaceId,
            GroupTypeId = groupTypeId,
            Name = name.Trim(),
            Description = description?.Trim(),
            CreatedByUserId = createdByUserId,
            JoinCode = GenerateJoinCode()
        };

    public string RegenerateJoinCode() { JoinCode = GenerateJoinCode(); Touch(); return JoinCode; }

    private static string GenerateJoinCode() =>
        Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    public void Update(string name, string? description) { Name = name.Trim(); Description = description?.Trim(); Touch(); }

    public void Rename(string name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.Length > 100)
            throw new InvalidOperationException("שם הקבוצה חייב להיות בין 1 ל-100 תווים.");
        Name = trimmed;
        Touch();
    }

    public void UpdateSettings(int solverHorizonDays, DateTime? solverStartDateTime = null) { SolverHorizonDays = Math.Clamp(solverHorizonDays, 1, 7); SolverStartDateTime = solverStartDateTime; Touch(); }
    public void SetAutoPublish(bool autoPublish) { AutoPublish = autoPublish; Touch(); }
    public void SetClosedBase(bool value) { IsClosedBase = value; Touch(); }

    public void SetMinRestBetweenShifts(int hours)
    {
        if (hours < 0 || hours > 24)
            throw new InvalidOperationException("שעות מנוחה מינימלית בין משמרות חייבות להיות בין 0 ל-24.");
        MinRestBetweenShiftsHours = hours;
        Touch();
    }

    public void Deactivate() { IsActive = false; Touch(); }

    public void SoftDelete() { DeletedAt = DateTime.UtcNow; Touch(); }
    public void Restore() { DeletedAt = null; Touch(); }
}
