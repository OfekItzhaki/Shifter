using FluentValidation;
using Jobuler.Application.Common;
using Jobuler.Domain.Scheduling;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Jobuler.Application.Scheduling.Commands;

public record TriggerRegenerationCommand(
    Guid SpaceId,
    Guid GroupId,
    Guid RequestedByUserId) : IRequest<Guid>;

public class TriggerRegenerationValidator : AbstractValidator<TriggerRegenerationCommand>
{
    public TriggerRegenerationValidator()
    {
        RuleFor(x => x.SpaceId).NotEmpty();
        RuleFor(x => x.GroupId).NotEmpty();
        RuleFor(x => x.RequestedByUserId).NotEmpty();
    }
}

public class TriggerRegenerationCommandHandler : IRequestHandler<TriggerRegenerationCommand, Guid>
{
    private readonly AppDbContext _db;
    private readonly ISolverJobQueue _queue;
    private readonly ITimezoneResolver _timezoneResolver;
    private readonly int _solverTimeoutSeconds;
    private readonly int _staleGracePeriodMinutes;

    public TriggerRegenerationCommandHandler(
        AppDbContext db,
        ISolverJobQueue queue,
        ITimezoneResolver timezoneResolver,
        IConfiguration configuration)
    {
        _db = db;
        _queue = queue;
        _timezoneResolver = timezoneResolver;
        _solverTimeoutSeconds = int.TryParse(configuration["Solver:TimeoutSeconds"], out var t)
            ? t
            : SchedulingConstants.DefaultSolverTimeoutSeconds;
        _staleGracePeriodMinutes = int.TryParse(configuration["Solver:StaleGracePeriodMinutes"], out var g)
            ? g
            : 5;
    }

    public async Task<Guid> Handle(TriggerRegenerationCommand request, CancellationToken ct)
    {
        // 1. Set RLS session variables
        if (_db.Database.IsRelational())
        {
            await _db.Database.ExecuteSqlRawAsync(
                "SELECT set_config('app.current_space_id', {0}, TRUE), set_config('app.current_user_id', {1}, TRUE)",
                request.SpaceId.ToString(),
                request.RequestedByUserId.ToString());
        }

        // 2. Check group subscription status — reject with 402 if trial expired and no active subscription
        var subscription = await _db.GroupSubscriptions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.GroupId == request.GroupId && s.SpaceId == request.SpaceId, ct);

        if (subscription != null && !subscription.IsActive)
        {
            throw new PaymentRequiredException(
                "תקופת הניסיון הסתיימה. שדרג את התוכנית כדי להפעיל סידור מחדש.");
        }

        // 3. Check for in-progress regeneration runs for this group (status Queued or Running)
        var staleThreshold = DateTime.UtcNow.AddSeconds(-_solverTimeoutSeconds).AddMinutes(-_staleGracePeriodMinutes);

        var inProgressRuns = await _db.ScheduleRuns
            .Where(r => r.SpaceId == request.SpaceId
                && r.GroupId == request.GroupId
                && r.TriggerType == ScheduleRunTrigger.Regeneration
                && (r.Status == ScheduleRunStatus.Queued || r.Status == ScheduleRunStatus.Running))
            .ToListAsync(ct);

        // 4. Handle stale runs: if a run has been "Running" longer than solver timeout + grace period, mark it failed
        foreach (var run in inProgressRuns.ToList())
        {
            if (run.Status == ScheduleRunStatus.Running
                && run.StartedAt.HasValue
                && run.StartedAt.Value < staleThreshold)
            {
                run.MarkFailed("Timed out: run exceeded solver timeout plus grace period.");
                inProgressRuns.Remove(run);
            }
        }

        // If there are still active (non-stale) runs, reject with 409
        if (inProgressRuns.Count > 0)
        {
            throw new ConflictException(
                "A regeneration run is already in progress for this group.");
        }

        // 5. Find current published version for the group — reject with 400 if none exists
        var publishedVersion = await _db.ScheduleVersions
            .AsNoTracking()
            .Where(v => v.SpaceId == request.SpaceId && v.Status == ScheduleVersionStatus.Published)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(ct);

        if (publishedVersion is null)
        {
            throw new InvalidOperationException(
                "No published version exists for this group. Generate a schedule first.");
        }

        // 6. Create ScheduleRun with TriggerType = Regeneration
        var scheduleRun = ScheduleRun.Create(
            request.SpaceId,
            ScheduleRunTrigger.Regeneration,
            publishedVersion.Id,
            request.RequestedByUserId,
            request.GroupId);

        _db.ScheduleRuns.Add(scheduleRun);
        await _db.SaveChangesAsync(ct);

        // 7. Enqueue SolverJobMessage with triggerMode = "regeneration", startTime = today in space timezone
        var startTime = ResolveSpaceToday(request.SpaceId);

        await _queue.EnqueueAsync(new SolverJobMessage(
            scheduleRun.Id,
            request.SpaceId,
            "regeneration",
            publishedVersion.Id,
            request.RequestedByUserId,
            request.GroupId,
            startTime), ct);

        // 8. Return run.Id
        return scheduleRun.Id;
    }

    /// <summary>
    /// Resolves "today" in the space's timezone by looking up the space owner's
    /// country/state and using the timezone resolver. Falls back to Asia/Jerusalem.
    /// </summary>
    private DateTime ResolveSpaceToday(Guid spaceId)
    {
        // Resolve space owner's timezone synchronously from cached data
        var ownerUser = _db.Spaces
            .AsNoTracking()
            .Where(s => s.Id == spaceId)
            .Join(_db.Users, s => s.OwnerUserId, u => u.Id, (s, u) => u)
            .FirstOrDefault();

        string ianaTimezoneId = "Asia/Jerusalem"; // default fallback
        if (ownerUser != null)
        {
            var resolution = _timezoneResolver.Resolve(ownerUser.CountryCode, ownerUser.StateCode);
            ianaTimezoneId = resolution.IanaTimezoneId;
        }

        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(ianaTimezoneId);
            var nowInTz = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
            // Return midnight of today in that timezone, expressed as UTC
            var todayLocal = nowInTz.Date;
            return todayLocal;
        }
        catch (TimeZoneNotFoundException)
        {
            // Fallback: use UTC today
            return DateTime.UtcNow.Date;
        }
    }
}
