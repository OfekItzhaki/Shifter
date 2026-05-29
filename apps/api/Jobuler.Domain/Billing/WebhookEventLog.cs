using Jobuler.Domain.Common;

namespace Jobuler.Domain.Billing;

public class WebhookEventLog : Entity
{
    public string EventId { get; private set; } = "";
    public string EventType { get; private set; } = "";
    public DateTime ProcessedAt { get; private set; }
    public bool ProcessedSuccessfully { get; private set; }

    private WebhookEventLog() { }

    public static WebhookEventLog Create(string eventId, string eventType) => new()
    {
        EventId = eventId,
        EventType = eventType,
        ProcessedAt = DateTime.UtcNow,
        ProcessedSuccessfully = false
    };

    public void MarkSuccessful()
    {
        ProcessedSuccessfully = true;
    }
}
