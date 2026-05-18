using FluentValidation;
using Jobuler.Application.Common;
using Jobuler.Domain.Scheduling;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Scheduling.Commands;

public record DismissRecommendationCommand(
    Guid SpaceId,
    Guid RecommendationId,
    Guid UserId) : IRequest;

public class DismissRecommendationCommandValidator : AbstractValidator<DismissRecommendationCommand>
{
    public DismissRecommendationCommandValidator()
    {
        RuleFor(x => x.SpaceId).NotEmpty();
        RuleFor(x => x.RecommendationId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
    }
}

public class DismissRecommendationCommandHandler : IRequestHandler<DismissRecommendationCommand>
{
    private readonly AppDbContext _db;
    private readonly IPermissionService _permissions;

    public DismissRecommendationCommandHandler(AppDbContext db, IPermissionService permissions)
    {
        _db = db;
        _permissions = permissions;
    }

    public async Task Handle(DismissRecommendationCommand req, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(req.UserId, req.SpaceId, Permissions.TasksManage, ct);

        var recommendation = await _db.DoubleShiftRecommendations
            .FirstOrDefaultAsync(r => r.Id == req.RecommendationId && r.SpaceId == req.SpaceId, ct)
            ?? throw new KeyNotFoundException("Recommendation not found.");

        recommendation.Dismiss(req.UserId);
        await _db.SaveChangesAsync(ct);
    }
}
