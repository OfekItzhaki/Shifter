using Jobuler.Domain.Common;

namespace Jobuler.Domain.Notifications;

public class Notification : Entity, ITenantScoped
{
    public Guid SpaceId { get; private set; }
    public Guid UserId { get; private set; }
    public string EventType { get; private set; } = default!;
    public string Title { get; private set; } = default!;
    public string Body { get; private set; } = default!;
    public string? MetadataJson { get; private set; }
    public bool IsRead { get; private set; }
    public DateTime? ReadAt { get; private set; }

    private Notification() { }

    public static Notification Create(
        Guid spaceId, Guid userId, string eventType,
        string title, string body, string? metadataJson = null) =>
        new()
        {
            SpaceId = spaceId,
            UserId = userId,
            EventType = eventType,
            Title = title,
            Body = body,
            MetadataJson = metadataJson
        };

    public void MarkRead()
    {
        IsRead = true;
        ReadAt = DateTime.UtcNow;
    }
}
