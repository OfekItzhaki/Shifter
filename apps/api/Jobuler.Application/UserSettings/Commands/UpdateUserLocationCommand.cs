using Jobuler.Application.Common;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.UserSettings.Commands;

/// <summary>
/// Updates a user's geographic location (Country/State) and returns the resolved timezone.
/// The timezone is derived from the geographic selection via ITimezoneResolver.
/// </summary>
public record UpdateUserLocationCommand(
    Guid UserId,
    string CountryCode,
    string? StateCode
) : IRequest<TimezoneResolution>;

public class UpdateUserLocationHandler : IRequestHandler<UpdateUserLocationCommand, TimezoneResolution>
{
    private readonly AppDbContext _db;
    private readonly ITimezoneResolver _timezoneResolver;

    public UpdateUserLocationHandler(AppDbContext db, ITimezoneResolver timezoneResolver)
    {
        _db = db;
        _timezoneResolver = timezoneResolver;
    }

    public async Task<TimezoneResolution> Handle(UpdateUserLocationCommand req, CancellationToken ct)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == req.UserId, ct)
            ?? throw new KeyNotFoundException("User not found");

        user.UpdateLocation(req.CountryCode, req.StateCode);
        await _db.SaveChangesAsync(ct);

        var resolution = _timezoneResolver.Resolve(user.CountryCode, user.StateCode);
        return resolution;
    }
}
