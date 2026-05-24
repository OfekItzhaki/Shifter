namespace Jobuler.Domain.Spaces;

/// <summary>
/// Four-tier permission hierarchy for space members.
/// Higher numeric value = higher authority.
/// </summary>
public enum SpacePermissionLevel
{
    Member = 0,
    Admin = 1,
    GroupOwner = 2,
    SpaceOwner = 3
}
