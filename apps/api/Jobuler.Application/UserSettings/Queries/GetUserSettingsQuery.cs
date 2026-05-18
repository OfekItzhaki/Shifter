using Jobuler.Application.Common;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.UserSettings.Queries;

public record UserSettingsDto(
    string? CountryCode,
    string? StateCode,
    string TimezoneId,
    int TimezoneOffsetMinutes,
    string TimeFormat);

public record GetUserSettingsQuery(Guid UserId) : IRequest<UserSettingsDto>;

public class GetUserSettingsQueryHandler : IRequestHandler<GetUserSettingsQuery, UserSettingsDto>
{
    private readonly AppDbContext _db;
    private readonly ITimezoneResolver _timezoneResolver;

    public GetUserSettingsQueryHandler(AppDbContext db, ITimezoneResolver timezoneResolver)
    {
        _db = db;
        _timezoneResolver = timezoneResolver;
    }

    public async Task<UserSettingsDto> Handle(GetUserSettingsQuery request, CancellationToken cancellationToken)
    {
        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken)
            ?? throw new KeyNotFoundException("User not found");

        var resolution = _timezoneResolver.Resolve(user.CountryCode, user.StateCode);

        return new UserSettingsDto(
            CountryCode: user.CountryCode,
            StateCode: user.StateCode,
            TimezoneId: resolution.IanaTimezoneId,
            TimezoneOffsetMinutes: resolution.OffsetMinutes,
            TimeFormat: "24h");
    }
}
