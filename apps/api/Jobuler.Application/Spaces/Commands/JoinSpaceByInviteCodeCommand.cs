using MediatR;

namespace Jobuler.Application.Spaces.Commands;

public record JoinSpaceByInviteCodeCommand(string InviteCode, Guid UserId) : IRequest<JoinSpaceResult>;

public record JoinSpaceResult(Guid SpaceId, string SpaceName, bool AlreadyMember);
