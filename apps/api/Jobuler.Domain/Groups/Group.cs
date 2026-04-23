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

    private Group() { }

    public static Group Create(Guid spaceId, Guid? groupTypeId, string name, string? description = null, Guid? createdByUserId = null) =>
        new()
        {
            SpaceId = spaceId,
            GroupTypeId = groupTypeId,
            Name = name.Trim(),
            Description = description?.Trim(),
            CreatedByUserId = createdByUserId
        };

    public void Update(string name, string? description) { Name = name.Trim(); Description = description?.Trim(); Touch(); }
    public void UpdateSettings(int solverHorizonDays) { SolverHorizonDays = Math.Clamp(solverHorizonDays, 1, 30); Touch(); }
    public void Deactivate() { IsActive = false; Touch(); }
}
