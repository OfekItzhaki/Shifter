using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Logs.Queries;

public record SystemLogDto(
    Guid Id, string Severity, string EventType, string Message,
    bool IsSensitive, Guid? ActorUserId, DateTime CreatedAt);

public record GetSystemLogsQuery(
    Guid SpaceId,
    string? Severity = null,
    string? EventType = null,
    DateTime? From = null,
    DateTime? To = null,
    bool IncludeSensitive = false,
    int Page = 1,
    int PageSize = 50) : IRequest<List<SystemLogDto>>;

public class GetSystemLogsQueryHandler : IRequestHandler<GetSystemLogsQuery, List<SystemLogDto>>
{
    private readonly AppDbContext _db;
    public GetSystemLogsQueryHandler(AppDbContext db) => _db = db;

    public async Task<List<SystemLogDto>> Handle(GetSystemLogsQuery req, CancellationToken ct)
    {
        var query = _db.SystemLogs.AsNoTracking()
            .Where(l => l.SpaceId == req.SpaceId);

        // Sensitive logs are hidden unless caller has logs.view_sensitive
        if (!req.IncludeSensitive)
            query = query.Where(l => !l.IsSensitive);

        if (!string.IsNullOrEmpty(req.Severity))
            query = query.Where(l => l.Severity == req.Severity.ToLower());

        if (!string.IsNullOrEmpty(req.EventType))
            query = query.Where(l => l.EventType.Contains(req.EventType));

        if (req.From.HasValue) query = query.Where(l => l.CreatedAt >= req.From.Value);
        if (req.To.HasValue)   query = query.Where(l => l.CreatedAt <= req.To.Value);

        return await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((req.Page - 1) * req.PageSize)
            .Take(req.PageSize)
            .Select(l => new SystemLogDto(
                l.Id, l.Severity, l.EventType, l.Message,
                l.IsSensitive, l.ActorUserId, l.CreatedAt))
            .ToListAsync(ct);
    }
}
