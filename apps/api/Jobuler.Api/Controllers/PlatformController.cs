using Jobuler.Application.Billing.Commands;
using Jobuler.Application.Organizations.Commands;
using Jobuler.Application.Organizations.Queries;
using Jobuler.Application.Platform.Commands;
using Jobuler.Application.Platform.Queries;
using Jobuler.Application.Scheduling.Commands;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

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

    private async Task<bool> IsPlatformAdminAsync(CancellationToken ct)
    {
        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == CurrentUserId, ct);
        return user?.IsPlatformAdmin == true;
    }

    /// <summary>
    /// GET /platform/stats
    /// Returns global platform metrics. Platform admin only.
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        if (!await IsPlatformAdminAsync(ct))
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
        if (!await IsPlatformAdminAsync(ct))
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
        if (!await IsPlatformAdminAsync(ct))
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
        if (!await IsPlatformAdminAsync(ct))
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
        if (!await IsPlatformAdminAsync(ct))
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
        if (!await IsPlatformAdminAsync(ct))
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
        if (!await IsPlatformAdminAsync(ct))
            return Forbid();

        var batchSize = request?.BatchSize ?? 100;
        var result = await _mediator.Send(new MigrateToSpaceBillingCommand(batchSize), ct);

        return Ok(result);
    }

    /// <summary>
    /// GET /platform/organizations
    /// Searches organization candidates for portability/migration review.
    /// Platform admin only.
    /// </summary>
    [HttpGet("organizations")]
    public async Task<IActionResult> SearchOrganizations(
        [FromQuery] string? search,
        [FromQuery] string? countryCode,
        [FromQuery] string? setupTemplate,
        [FromQuery] Jobuler.Domain.Organizations.OrganizationStatus? status,
        [FromQuery] int? limit,
        CancellationToken ct)
    {
        if (!await IsPlatformAdminAsync(ct))
            return Forbid();

        var result = await _mediator.Send(new SearchOrganizationsQuery(
            search,
            countryCode,
            setupTemplate,
            status,
            limit ?? 50), ct);
        return Ok(result);
    }

    /// <summary>
    /// GET /platform/organizations/{organizationId}/export-manifest
    /// Returns a dry-run manifest for organization relocation/export review.
    /// Platform admin only.
    /// </summary>
    [HttpGet("organizations/{organizationId:guid}/export-manifest")]
    public async Task<IActionResult> GetOrganizationExportManifest(
        Guid organizationId,
        CancellationToken ct)
    {
        if (!await IsPlatformAdminAsync(ct))
            return Forbid();

        var result = await _mediator.Send(new GetOrganizationExportManifestQuery(organizationId), ct);
        return Ok(result);
    }

    /// <summary>
    /// GET /platform/organizations/{organizationId}/export-package
    /// Downloads a JSON organization export package for dedicated-deployment migration.
    /// Platform admin only.
    /// </summary>
    [HttpGet("organizations/{organizationId:guid}/export-package")]
    public async Task<IActionResult> ExportOrganizationPackage(
        Guid organizationId,
        CancellationToken ct)
    {
        if (!await IsPlatformAdminAsync(ct))
            return Forbid();

        var result = await _mediator.Send(new ExportOrganizationPackageCommand(organizationId), ct);
        return File(result.Content, "application/json", result.FileName);
    }

    /// <summary>
    /// POST /platform/organizations/import/validate
    /// Dry-runs an organization export package against this deployment before import.
    /// Platform admin only.
    /// </summary>
    [HttpPost("organizations/import/validate")]
    public async Task<IActionResult> ValidateOrganizationImport(
        [FromBody] JsonElement package,
        CancellationToken ct)
    {
        if (!await IsPlatformAdminAsync(ct))
            return Forbid();

        var result = await _mediator.Send(
            new ValidateOrganizationImportPackageCommand(package.GetRawText()), ct);
        return Ok(result);
    }

    /// <summary>
    /// POST /platform/organizations/import
    /// Imports a previously validated organization export package into this deployment.
    /// Platform admin only.
    /// </summary>
    [HttpPost("organizations/import")]
    public async Task<IActionResult> ImportOrganization(
        [FromBody] ImportOrganizationRequest request,
        CancellationToken ct)
    {
        if (!await IsPlatformAdminAsync(ct))
            return Forbid();

        var result = await _mediator.Send(
            new ImportOrganizationPackageCommand(
                request.Package.GetRawText(),
                request.ConfirmImport),
            ct);
        return Ok(result);
    }

    /// <summary>
    /// PATCH /platform/organizations/{organizationId}
    /// Updates operator-controlled organization identity/signals.
    /// Platform admin only.
    /// </summary>
    [HttpPatch("organizations/{organizationId:guid}")]
    public async Task<IActionResult> UpdateOrganization(
        Guid organizationId,
        [FromBody] UpdateOrganizationRequest request,
        CancellationToken ct)
    {
        if (!await IsPlatformAdminAsync(ct))
            return Forbid();

        await _mediator.Send(new UpdateOrganizationCommand(
            organizationId,
            request.DisplayName,
            request.CountryCode,
            request.SetupTemplate,
            request.DefaultLocale,
            request.DefaultTimezoneId), ct);
        return NoContent();
    }

    /// <summary>
    /// GET /platform/organizations/{organizationId}/self-service-defaults
    /// Returns the organization-level self-service defaults template.
    /// Platform admin only.
    /// </summary>
    [HttpGet("organizations/{organizationId:guid}/self-service-defaults")]
    public async Task<IActionResult> GetOrganizationSelfServiceDefaults(
        Guid organizationId,
        CancellationToken ct)
    {
        if (!await IsPlatformAdminAsync(ct))
            return Forbid();

        var result = await _mediator.Send(
            new GetOrganizationSelfServiceDefaultsQuery(organizationId), ct);
        return Ok(result);
    }

    /// <summary>
    /// PUT /platform/organizations/{organizationId}/self-service-defaults
    /// Updates the organization-level template used before install defaults.
    /// Platform admin only.
    /// </summary>
    [HttpPut("organizations/{organizationId:guid}/self-service-defaults")]
    public async Task<IActionResult> UpdateOrganizationSelfServiceDefaults(
        Guid organizationId,
        [FromBody] UpdateOrganizationSelfServiceDefaultsRequest request,
        CancellationToken ct)
    {
        if (!await IsPlatformAdminAsync(ct))
            return Forbid();

        var result = await _mediator.Send(new UpdateOrganizationSelfServiceDefaultsCommand(
            organizationId,
            request.MinShiftsPerCycle,
            request.MaxShiftsPerCycle,
            request.RequestWindowOpenOffsetHours,
            request.RequestWindowCloseOffsetHours,
            request.CancellationCutoffHours,
            request.MaxAbsencesPerCycle,
            request.MaxLateCancellationsPerCycle,
            request.LateCancellationWindowHours,
            request.WaitlistOfferMinutes,
            request.CycleDurationDays,
            request.AllowMemberShiftClaims,
            request.AllowWaitlist,
            request.AllowShiftChangeRequests,
            request.AllowAbsenceReports,
            request.AllowShiftSwaps), ct);

        return Ok(result);
    }

    /// <summary>
    /// POST /platform/organizations/{organizationId}/spaces/{spaceId}
    /// Moves a verified space into an official organization.
    /// Platform admin only.
    /// </summary>
    [HttpPost("organizations/{organizationId:guid}/spaces/{spaceId:guid}")]
    public async Task<IActionResult> MoveSpaceToOrganization(
        Guid organizationId,
        Guid spaceId,
        CancellationToken ct)
    {
        if (!await IsPlatformAdminAsync(ct))
            return Forbid();

        await _mediator.Send(new MoveSpaceToOrganizationCommand(spaceId, organizationId), ct);
        return NoContent();
    }

    /// <summary>
    /// POST /platform/organizations/{organizationId}/relocate
    /// Disables an organization after confirmed migration to a dedicated deployment.
    /// Platform admin only.
    /// </summary>
    [HttpPost("organizations/{organizationId:guid}/relocate")]
    public async Task<IActionResult> MarkOrganizationRelocated(
        Guid organizationId,
        [FromBody] MarkOrganizationRelocatedRequest request,
        CancellationToken ct)
    {
        if (!await IsPlatformAdminAsync(ct))
            return Forbid();

        await _mediator.Send(new MarkOrganizationRelocatedCommand(
            organizationId,
            request.DedicatedDeploymentKey,
            request.RetentionDays ?? 90), ct);
        return NoContent();
    }

    /// <summary>
    /// POST /platform/organizations/{organizationId}/restore
    /// Restores a relocated organization during the rollback window.
    /// Platform admin only.
    /// </summary>
    [HttpPost("organizations/{organizationId:guid}/restore")]
    public async Task<IActionResult> RestoreOrganization(Guid organizationId, CancellationToken ct)
    {
        if (!await IsPlatformAdminAsync(ct))
            return Forbid();

        await _mediator.Send(new RestoreRelocatedOrganizationCommand(organizationId), ct);
        return NoContent();
    }

    /// <summary>
    /// POST /platform/organizations/{organizationId}/purge-pending
    /// Marks a relocated organization ready for final deletion/anonymization.
    /// Platform admin only.
    /// </summary>
    [HttpPost("organizations/{organizationId:guid}/purge-pending")]
    public async Task<IActionResult> MarkOrganizationPurgePending(Guid organizationId, CancellationToken ct)
    {
        if (!await IsPlatformAdminAsync(ct))
            return Forbid();

        await _mediator.Send(new MarkOrganizationPurgePendingCommand(organizationId), ct);
        return NoContent();
    }

    /// <summary>
    /// POST /platform/organizations/{organizationId}/purge
    /// Permanently deletes a purge-pending relocated organization after retention review.
    /// Platform admin only.
    /// </summary>
    [HttpPost("organizations/{organizationId:guid}/purge")]
    public async Task<IActionResult> PurgeOrganization(
        Guid organizationId,
        [FromBody] PurgeOrganizationRequest request,
        CancellationToken ct)
    {
        if (!await IsPlatformAdminAsync(ct))
            return Forbid();

        if (!request.ConfirmPermanentDeletion)
            return BadRequest(new { error = "confirmPermanentDeletion is required." });

        var result = await _mediator.Send(new PurgeOrganizationCommand(organizationId), ct);
        return Ok(result);
    }

    /// <summary>
    /// PUT /platform/organizations/{organizationId}/subscription
    /// Sets organization-level billing coverage for private or enterprise customers.
    /// Platform admin only.
    /// </summary>
    [HttpPut("organizations/{organizationId:guid}/subscription")]
    public async Task<IActionResult> SetOrganizationSubscription(
        Guid organizationId,
        [FromBody] SetOrganizationSubscriptionRequest request,
        CancellationToken ct)
    {
        if (!await IsPlatformAdminAsync(ct))
            return Forbid();

        await _mediator.Send(new SetOrganizationSubscriptionCommand(
            organizationId,
            request.BillingMode,
            request.TierId,
            request.CurrentPeriodStart,
            request.CurrentPeriodEnd,
            request.AutoRenew,
            request.ProviderSubscriptionId,
            request.ProviderCustomerId,
            request.CoveredSpaceLimit,
            request.CoveredMemberLimit), ct);
        return NoContent();
    }

    /// <summary>
    /// POST /platform/organizations/{organizationId}/subscription/cancel
    /// Cancels organization-level billing coverage.
    /// Platform admin only.
    /// </summary>
    [HttpPost("organizations/{organizationId:guid}/subscription/cancel")]
    public async Task<IActionResult> CancelOrganizationSubscription(
        Guid organizationId,
        CancellationToken ct)
    {
        if (!await IsPlatformAdminAsync(ct))
            return Forbid();

        await _mediator.Send(new CancelOrganizationSubscriptionCommand(organizationId), ct);
        return NoContent();
    }
}

public record PlatformSettingsResponse(int PlatformTimeoutMinutes);

public record UpdatePlatformSettingsRequest(int PlatformTimeoutMinutes);

public record MigrateBillingRequest(int? BatchSize = null);

public record UpdateOrganizationRequest(
    string DisplayName,
    string? CountryCode,
    string? SetupTemplate,
    string? DefaultLocale,
    string? DefaultTimezoneId);

public record UpdateOrganizationSelfServiceDefaultsRequest(
    int MinShiftsPerCycle,
    int MaxShiftsPerCycle,
    int RequestWindowOpenOffsetHours,
    int RequestWindowCloseOffsetHours,
    int CancellationCutoffHours,
    int MaxAbsencesPerCycle,
    int MaxLateCancellationsPerCycle,
    int LateCancellationWindowHours,
    int WaitlistOfferMinutes,
    int CycleDurationDays,
    bool AllowMemberShiftClaims,
    bool AllowWaitlist,
    bool AllowShiftChangeRequests,
    bool AllowAbsenceReports,
    bool AllowShiftSwaps);

public record ImportOrganizationRequest(JsonElement Package, bool ConfirmImport);

public record MarkOrganizationRelocatedRequest(
    string DedicatedDeploymentKey,
    int? RetentionDays = null);

public record PurgeOrganizationRequest(bool ConfirmPermanentDeletion);

public record SetOrganizationSubscriptionRequest(
    Jobuler.Domain.Billing.OrganizationBillingMode BillingMode,
    string TierId,
    DateTime CurrentPeriodStart,
    DateTime? CurrentPeriodEnd,
    bool AutoRenew,
    string? ProviderSubscriptionId = null,
    string? ProviderCustomerId = null,
    int? CoveredSpaceLimit = null,
    int? CoveredMemberLimit = null);
