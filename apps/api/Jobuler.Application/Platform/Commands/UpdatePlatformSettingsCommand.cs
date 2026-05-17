using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Platform.Commands;

/// <summary>
/// Updates platform-level settings. Requires platform admin (super-admin) permission.
/// Currently supports updating the platform session timeout duration.
/// </summary>
public record UpdatePlatformSettingsCommand(
    Guid UserId,
    int PlatformTimeoutMinutes
) : IRequest;

public class UpdatePlatformSettingsCommandHandler : IRequestHandler<UpdatePlatformSettingsCommand>
{
    private readonly AppDbContext _db;

    public UpdatePlatformSettingsCommandHandler(AppDbContext db) => _db = db;

    public async Task Handle(UpdatePlatformSettingsCommand request, CancellationToken ct)
    {
        // Platform admin check
        var user = await _db.Users.FindAsync(new object[] { request.UserId }, ct);
        if (user == null || !user.IsPlatformAdmin)
            throw new UnauthorizedAccessException("Platform admin access required.");

        // Validate range [5, 120]
        if (request.PlatformTimeoutMinutes < 5 || request.PlatformTimeoutMinutes > 120)
            throw new InvalidOperationException("Platform timeout must be between 5 and 120 minutes.");

        // Load platform setting by key and update
        var setting = await _db.PlatformSettings
            .FirstOrDefaultAsync(s => s.Key == "platform_timeout_minutes", ct);

        if (setting is null)
            throw new KeyNotFoundException("Platform timeout setting not found.");

        setting.UpdateValue(request.PlatformTimeoutMinutes.ToString());
        await _db.SaveChangesAsync(ct);
    }
}
