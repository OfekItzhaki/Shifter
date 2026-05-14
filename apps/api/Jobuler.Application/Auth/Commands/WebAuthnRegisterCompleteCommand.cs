using FluentValidation;
using Jobuler.Domain.Identity;
using Jobuler.Infrastructure.Persistence;
using MediatR;

namespace Jobuler.Application.Auth.Commands;

/// <summary>
/// Completes WebAuthn credential registration by verifying the attestation response
/// and persisting the new credential.
/// </summary>
public record WebAuthnRegisterCompleteCommand(
    Guid UserId,
    string ChallengeId,
    string AttestationResponseJson,
    string? Nickname) : IRequest<Guid>;

public class WebAuthnRegisterCompleteCommandValidator : AbstractValidator<WebAuthnRegisterCompleteCommand>
{
    public WebAuthnRegisterCompleteCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.ChallengeId).NotEmpty();
        RuleFor(x => x.AttestationResponseJson).NotEmpty();
        RuleFor(x => x.Nickname)
            .MaximumLength(100)
            .When(x => x.Nickname is not null);
    }
}

public class WebAuthnRegisterCompleteCommandHandler
    : IRequestHandler<WebAuthnRegisterCompleteCommand, Guid>
{
    private readonly AppDbContext _db;
    private readonly IWebAuthnService _webAuthn;

    public WebAuthnRegisterCompleteCommandHandler(AppDbContext db, IWebAuthnService webAuthn)
    {
        _db = db;
        _webAuthn = webAuthn;
    }

    public async Task<Guid> Handle(WebAuthnRegisterCompleteCommand request, CancellationToken ct)
    {
        // Verify the attestation response against the stored challenge
        var credentialData = await _webAuthn.CompleteRegistrationAsync(
            request.ChallengeId,
            request.AttestationResponseJson,
            ct);

        // Create the domain entity
        var credential = WebAuthnCredential.Create(
            request.UserId,
            credentialData.CredentialId,
            credentialData.PublicKey,
            credentialData.SignCount,
            credentialData.Transports,
            request.Nickname);

        _db.WebAuthnCredentials.Add(credential);
        await _db.SaveChangesAsync(ct);

        return credential.Id;
    }
}
