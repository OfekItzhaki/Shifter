using MediatR;

namespace Jobuler.Application.Auth.Queries;

public record ExportMyDataQuery(Guid UserId) : IRequest<UserDataExport>;

public record UserDataExport(
    UserProfileData Profile,
    List<UserGroupMembership> Groups,
    List<UserAssignment> Assignments,
    List<UserNotification> Notifications
);

public record UserProfileData(
    string Email,
    string? DisplayName,
    string? PhoneNumber,
    string? Birthday,
    string? ProfileImageUrl,
    DateTime CreatedAt
);

public record UserGroupMembership(
    string GroupName,
    string SpaceName,
    string Role,
    DateTime JoinedAt
);

public record UserAssignment(
    string GroupName,
    string TaskName,
    DateTime StartsAt,
    DateTime EndsAt
);

public record UserNotification(
    string Title,
    string Body,
    string EventType,
    DateTime CreatedAt,
    bool IsRead
);
