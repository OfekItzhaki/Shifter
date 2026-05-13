using FluentValidation;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Auth.Commands;

/// <summary>
/// Initiates WebAuthn credential registration for the authenticated user.
/// Returns options JSON for navigator.credentials.create() and a challenge ID.
/// </summary>
public record WebAuthnRegisterOptionsCommand(Guid UserId) : IRequest<WebAuthnRegisterOptionsResult>;

public record WebAuthnRegisterOptionsResult(string OptionsJson, string ChallengeId);

public class WebAuthnRegisterOptionsCommandValidator : AbstractValidator<WebAuthnRegisterOptionsCommand>
{
    public WebAuthnRegisterOptionsCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
    }
}

public class WebAuthnRegisterOptionsCommandHandler
    : IRequestHandler<WebAuthnRegisterOptionsCommand, WebAuthnRegisterOptionsResult>
{
    private readonly AppDbContext _db;
    private readonly IWebAuthnService _webAuthn;

    public WebAuthnRegisterOptionsCommandHandler(AppDbContext db, IWebAuthnService webAuthn)
    {
        _db = db;
        _webAuthn = webAuthn;
    }

    public async Task<WebAuthnRegisterOptionsResult> Handle(
        WebAuthnRegisterOptionsCommand request, CancellationToken ct)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == request.UserId && u.IsActive, ct)
            ?? throw new KeyNotFoundException("User not found.");

        // Load existing credential IDs to exclude (prevent duplicate registrations)
        var existingCredentialIds = await _db.WebAuthnCredentials
            .Where(c => c.UserId == request.UserId)
            .Select(c => c.CredentialId)
            .ToListAsync(ct);

        var result = await _webAuthn.GenerateRegistrationOptionsAsync(
            user.Id,
            user.Email,
            user.DisplayName,
            existingCredentialIds,
            ct);

        return new WebAuthnRegisterOptionsResult(result.OptionsJson, result.ChallengeId);
    }
}
