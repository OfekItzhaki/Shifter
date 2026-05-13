using MediatR;

namespace Jobuler.Application.HomeLeave.Commands;

public record PreviewHomeLeaveCommand(
    Guid SpaceId,
    Guid GroupId,
    int BalanceValue,
    Guid RequestingUserId) : IRequest<HomeLeavePreviewResponse>;

public record HomeLeavePreviewResponse(
    string Status,           // "optimal" | "feasible" | "no_solution"
    int PeopleHomeCount,
    int PeopleAtBaseCount,
    int TotalHomeLeaveSlots,
    List<CoverageGapDto> CoverageGaps,
    decimal FairnessSpread,
    int SolverTimeMs);

public record CoverageGapDto(
    string StartsAt,
    string EndsAt,
    int AvailableCount);
