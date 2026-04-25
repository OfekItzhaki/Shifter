using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.People.Queries;

public record PersonSearchResultDto(
    Guid Id,
    string FullName,
    string? DisplayName,
    string? PhoneNumber,
    Guid? LinkedUserId,
    string InvitationStatus);

public record SearchPeopleQuery(
    Guid SpaceId,
    string Query) : IRequest<List<PersonSearchResultDto>>;

public class SearchPeopleQueryHandler : IRequestHandler<SearchPeopleQuery, List<PersonSearchResultDto>>
{
    private readonly AppDbContext _db;
    public SearchPeopleQueryHandler(AppDbContext db) => _db = db;

    public async Task<List<PersonSearchResultDto>> Handle(SearchPeopleQuery req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Query) || req.Query.Trim().Length < 2)
            throw new InvalidOperationException("Search query must be at least 2 characters.");

        var q = req.Query.Trim().ToLowerInvariant();

        var people = await _db.People.AsNoTracking()
            .Where(p => p.SpaceId == req.SpaceId && p.IsActive &&
                (p.FullName.ToLower().Contains(q) ||
                 (p.DisplayName != null && p.DisplayName.ToLower().Contains(q)) ||
                 (p.PhoneNumber != null && p.PhoneNumber.Contains(q))))
            .OrderBy(p => p.FullName)
            .Take(20)
            .ToListAsync(ct);

        return people.Select(p => new PersonSearchResultDto(
            p.Id,
            p.FullName,
            p.DisplayName,
            p.PhoneNumber,
            p.LinkedUserId,
            p.InvitationStatus ?? "accepted")).ToList();
    }
}
