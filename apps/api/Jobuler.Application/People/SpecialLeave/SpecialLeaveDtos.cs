namespace Jobuler.Application.People.SpecialLeave;

public record SpecialLeaveRequestDto(
    Guid Id,
    Guid SpaceId,
    Guid PersonId,
    string PersonName,
    DateTime StartsAt,
    DateTime EndsAt,
    string Reason,
    string Status,
    Guid RequestedByUserId,
    Guid? ProcessedByUserId,
    DateTime? ProcessedAt,
    string? AdminNote,
    Guid? PresenceWindowId,
    DateTime CreatedAt,
    DateTime UpdatedAt);
