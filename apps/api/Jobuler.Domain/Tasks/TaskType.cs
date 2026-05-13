using Jobuler.Domain.Common;

namespace Jobuler.Domain.Tasks;

public class TaskType : AuditableEntity, ITenantScoped
{
    public Guid SpaceId { get; private set; }
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public TaskBurdenLevel BurdenLevel { get; private set; } = TaskBurdenLevel.Normal;
    public int DefaultPriority { get; private set; } = 5;
    public bool AllowsOverlap { get; private set; } = false;
    public bool IsActive { get; private set; } = true;
    public Guid? CreatedByUserId { get; private set; }

    private TaskType() { }

    public static TaskType Create(
        Guid spaceId, string name, TaskBurdenLevel burdenLevel,
        Guid createdByUserId, string? description = null,
        int defaultPriority = 5, bool allowsOverlap = false) =>
        new()
        {
            SpaceId = spaceId,
            Name = name.Trim(),
            Description = description?.Trim(),
            BurdenLevel = burdenLevel,
            DefaultPriority = defaultPriority,
            AllowsOverlap = allowsOverlap,
            CreatedByUserId = createdByUserId
        };

    public void Update(string name, string? description, TaskBurdenLevel burdenLevel,
        int defaultPriority, bool allowsOverlap)
    {
        Name = name.Trim();
        Description = description?.Trim();
        BurdenLevel = burdenLevel;
        DefaultPriority = defaultPriority;
        AllowsOverlap = allowsOverlap;
        Touch();
    }

    public void Deactivate() { IsActive = false; Touch(); }
}
