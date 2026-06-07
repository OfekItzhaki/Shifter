using Jobuler.Domain.Common;

namespace Jobuler.Domain.People;

public enum SpecialLeaveRequestStatus
{
    Pending,
    Approved,
    Rejected,
    Cancelled
}

public class SpecialLeaveRequest : AuditableEntity, ITenantScoped
{
    public Guid SpaceId { get; private set; }
    public Guid PersonId { get; private set; }
    public DateTime StartsAt { get; private set; }
    public DateTime EndsAt { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public SpecialLeaveRequestStatus Status { get; private set; }
    public Guid RequestedByUserId { get; private set; }
    public Guid? ProcessedByUserId { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public string? AdminNote { get; private set; }
    public Guid? PresenceWindowId { get; private set; }

    private SpecialLeaveRequest() { }

    public static SpecialLeaveRequest Create(
        Guid spaceId,
        Guid personId,
        DateTime startsAt,
        DateTime endsAt,
        string reason,
        Guid requestedByUserId)
    {
        if (endsAt <= startsAt)
            throw new ArgumentException("EndsAt must be after StartsAt.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason is required.");

        var trimmedReason = reason.Trim();
        if (trimmedReason.Length > 500)
            throw new ArgumentException("Reason must be 500 characters or less.");

        return new SpecialLeaveRequest
        {
            SpaceId = spaceId,
            PersonId = personId,
            StartsAt = startsAt,
            EndsAt = endsAt,
            Reason = trimmedReason,
            RequestedByUserId = requestedByUserId,
            Status = SpecialLeaveRequestStatus.Pending
        };
    }

    public void Approve(Guid processedByUserId, Guid presenceWindowId, string? adminNote)
    {
        EnsurePending();
        Status = SpecialLeaveRequestStatus.Approved;
        ProcessedByUserId = processedByUserId;
        ProcessedAt = DateTime.UtcNow;
        PresenceWindowId = presenceWindowId;
        AdminNote = NormalizeAdminNote(adminNote);
        Touch();
    }

    public void Reject(Guid processedByUserId, string? adminNote)
    {
        EnsurePending();
        Status = SpecialLeaveRequestStatus.Rejected;
        ProcessedByUserId = processedByUserId;
        ProcessedAt = DateTime.UtcNow;
        AdminNote = NormalizeAdminNote(adminNote);
        Touch();
    }

    public void Cancel()
    {
        EnsurePending();
        Status = SpecialLeaveRequestStatus.Cancelled;
        Touch();
    }

    private void EnsurePending()
    {
        if (Status != SpecialLeaveRequestStatus.Pending)
            throw new InvalidOperationException("Only pending special leave requests can be changed.");
    }

    private static string? NormalizeAdminNote(string? adminNote)
    {
        if (string.IsNullOrWhiteSpace(adminNote))
            return null;

        var trimmed = adminNote.Trim();
        return trimmed.Length <= 500 ? trimmed : trimmed[..500];
    }
}
