using Jobuler.Domain.Identity;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Auth.Commands;

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, Guid>
{
    private readonly AppDbContext _db;

    public RegisterCommandHandler(AppDbContext db) => _db = db;

    public async Task<Guid> Handle(RegisterCommand request, CancellationToken ct)
    {
        var exists = await _db.Users.AnyAsync(u => u.Email == request.Email.ToLowerInvariant().Trim(), ct);
        if (exists)
            throw new InvalidOperationException("Email already registered.");

        var hash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12);
        var user = User.Create(request.Email, request.DisplayName, hash, request.PreferredLocale, request.PhoneNumber, request.ProfileImageUrl, request.Birthday);

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        // Auto-create a personal space for the new user
        var displayName = request.DisplayName ?? request.Email.Split('@')[0];
        var spaceName = (request.PreferredLocale ?? "he") switch
        {
            "he" => $"המרחב של {displayName}",
            "ru" => $"Пространство {displayName}",
            _ => $"{displayName}'s Space",
        };
        var space = Jobuler.Domain.Spaces.Space.Create(spaceName, user.Id);
        _db.Spaces.Add(space);

        // Add user as space member
        var membership = Jobuler.Domain.Spaces.SpaceMembership.Create(space.Id, user.Id);
        _db.SpaceMemberships.Add(membership);
        await _db.SaveChangesAsync(ct);

        return user.Id;
    }
}
