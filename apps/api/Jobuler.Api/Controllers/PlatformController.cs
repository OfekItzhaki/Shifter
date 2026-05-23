using Jobuler.Application.Billing.Commands;
using Jobuler.Application.Platform.Commands;
using Jobuler.Application.Platform.Queries;
using Jobuler.Application.Scheduling.Commands;
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

    /// <summary>
    /// POST /platform/backfill/subscription-periods
    /// One-time backfill that creates initial subscription periods for all existing groups.
    /// Platform admin only. Idempotent — skips groups that already have a period.
    /// </summary>
    [HttpPost("backfill/subscription-periods")]
    public async Task<IActionResult> BackfillSubscriptionPeriods(CancellationToken ct)
    {
        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == CurrentUserId, ct);
        if (user?.IsPlatformAdmin != true)
            return Forbid();

        var result = await _mediator.Send(new BackfillSubscriptionPeriodsCommand(), ct);
        return Ok(result);
    }

    /// <summary>
    /// POST /platform/backfill/daily-snapshots
    /// One-time backfill that generates daily snapshots from existing published schedule versions.
    /// Platform admin only. Idempotent — skips existing snapshot rows.
    /// </summary>
    [HttpPost("backfill/daily-snapshots")]
    public async Task<IActionResult> BackfillDailySnapshots(CancellationToken ct)
    {
        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == CurrentUserId, ct);
        if (user?.IsPlatformAdmin != true)
            return Forbid();

        var result = await _mediator.Send(new BackfillDailySnapshotsCommand(), ct);
        return Ok(result);
    }

    /// <summary>
    /// POST /platform/backfill/cumulative-records
    /// One-time backfill that computes initial cumulative records from daily snapshots.
    /// Platform admin only. Idempotent — uses upsert pattern.
    /// </summary>
    [HttpPost("backfill/cumulative-records")]
    public async Task<IActionResult> BackfillCumulativeRecords(CancellationToken ct)
    {
        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == CurrentUserId, ct);
        if (user?.IsPlatformAdmin != true)
            return Forbid();

        var result = await _mediator.Send(new BackfillCumulativeRecordsCommand(), ct);
        return Ok(result);
    }

    /// <summary>
    /// GET /platform/settings
    /// Returns current platform settings including platformTimeoutMinutes.
    /// Platform admin only.
    /// </summary>
    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings(CancellationToken ct)
    {
        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == CurrentUserId, ct);
        if (user?.IsPlatformAdmin != true)
            return Forbid();

        var timeoutSetting = await _db.PlatformSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == "platform_timeout_minutes", ct);

        var timeoutMinutes = timeoutSetting is not null
            ? int.Parse(timeoutSetting.Value)
            : 15;

        return Ok(new PlatformSettingsResponse(timeoutMinutes));
    }

    /// <summary>
    /// PATCH /platform/settings
    /// Updates platform settings. Currently supports platformTimeoutMinutes.
    /// Platform admin only.
    /// </summary>
    [HttpPatch("settings")]
    public async Task<IActionResult> UpdateSettings(
        [FromBody] UpdatePlatformSettingsRequest request,
        CancellationToken ct)
    {
        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == CurrentUserId, ct);
        if (user?.IsPlatformAdmin != true)
            return Forbid();

        await _mediator.Send(new UpdatePlatformSettingsCommand(
            CurrentUserId,
            request.PlatformTimeoutMinutes
        ), ct);

        return NoContent();
    }

    /// <summary>
    /// POST /platform/billing/migrate
    /// One-time migration from group-level billing to space-level billing.
    /// Platform admin only. Accepts optional batchSize in body.
    /// </summary>
    [HttpPost("billing/migrate")]
    public async Task<IActionResult> MigrateToSpaceBilling(
        [FromBody] MigrateBillingRequest? request,
        CancellationToken ct)
    {
        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == CurrentUserId, ct);
        if (user?.IsPlatformAdmin != true)
            return Forbid();

        var batchSize = request?.BatchSize ?? 100;
        var result = await _mediator.Send(new MigrateToSpaceBillingCommand(batchSize), ct);

        return Ok(result);
    }
}

public record PlatformSettingsResponse(int PlatformTimeoutMinutes);

public record UpdatePlatformSettingsRequest(int PlatformTimeoutMinutes);

public record MigrateBillingRequest(int? BatchSize = null);
