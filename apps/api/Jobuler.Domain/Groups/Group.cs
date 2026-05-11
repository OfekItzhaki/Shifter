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
            throw new InvalidOperationException("Group name must be between 1 and 100 non-blank characters.");
        Name = trimmed;
        Touch();
    }

    public void UpdateSettings(int solverHorizonDays, DateTime? solverStartDateTime = null) { SolverHorizonDays = Math.Clamp(solverHorizonDays, 1, 90); SolverStartDateTime = solverStartDateTime; Touch(); }
    public void Deactivate() { IsActive = false; Touch(); }

    public void SoftDelete() { DeletedAt = DateTime.UtcNow; Touch(); }
    public void Restore() { DeletedAt = null; Touch(); }
}
