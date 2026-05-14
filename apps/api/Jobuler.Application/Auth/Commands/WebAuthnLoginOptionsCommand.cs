using MediatR;

namespace Jobuler.Application.Auth.Commands;

/// <summary>
/// Initiates WebAuthn authentication (anonymous — no auth required).
/// Returns options JSON for navigator.credentials.get() and a challenge ID.
/// </summary>
public record WebAuthnLoginOptionsCommand() : IRequest<WebAuthnLoginOptionsResult>;

public record WebAuthnLoginOptionsResult(string OptionsJson, string ChallengeId);

public class WebAuthnLoginOptionsCommandHandler
    : IRequestHandler<WebAuthnLoginOptionsCommand, WebAuthnLoginOptionsResult>
{
    private readonly IWebAuthnService _webAuthn;

    public WebAuthnLoginOptionsCommandHandler(IWebAuthnService webAuthn)
    {
        _webAuthn = webAuthn;
    }

    public async Task<WebAuthnLoginOptionsResult> Handle(
        WebAuthnLoginOptionsCommand request, CancellationToken ct)
    {
        var result = await _webAuthn.GenerateAuthenticationOptionsAsync(ct);

        return new WebAuthnLoginOptionsResult(result.OptionsJson, result.ChallengeId);
    }
}
