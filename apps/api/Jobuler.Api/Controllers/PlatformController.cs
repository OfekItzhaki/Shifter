using Jobuler.Application.Platform.Queries;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Jobuler.Api.Controllers;

[ApiController]
[Route("platform")]
[Authorize]
public class PlatformController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly AppDbContext _db;

    public PlatformController(IMediator mediator, AppDbContext db)
    {
        _mediator = mediator;
        _db = db;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// GET /platform/stats
    /// Returns global platform metrics. Platform admin only.
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == CurrentUserId, ct);
        if (user?.IsPlatformAdmin != true)
            return Forbid();

        var result = await _mediator.Send(new GetPlatformStatsQuery(), ct);
        return Ok(result);
    }
}
