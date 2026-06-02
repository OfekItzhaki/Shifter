using Jobuler.Application.Auth.Commands;
using Jobuler.Application.Auth.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace Jobuler.Api.Controllers;

[ApiController]
[Route("auth/webauthn")]
[EnableRateLimiting("auth")]
public class WebAuthnController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public WebAuthnController(IMediator mediator, IConfiguration configuration, IWebHostEnvironment environment)
    {
        _mediator = mediator;
        _configuration = configuration;
        _environment = environment;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>Get registration options for navigator.credentials.create().</summary>
    [HttpPost("register/options")]
    [Authorize]
    public async Task<IActionResult> RegisterOptions(CancellationToken ct)
    {
        var result = await _mediator.Send(new WebAuthnRegisterOptionsCommand(CurrentUserId), ct);
        return Ok(new { optionsJson = result.OptionsJson, challengeId = result.ChallengeId });
    }

    /// <summary>Complete registration by verifying the attestation response.</summary>
    [HttpPost("register/complete")]
    [Authorize]
    public async Task<IActionResult> RegisterComplete(
        [FromBody] RegisterCompleteRequest req, CancellationToken ct)
    {
        var credentialId = await _mediator.Send(
            new WebAuthnRegisterCompleteCommand(
                CurrentUserId,
                req.ChallengeId,
                req.AttestationResponseJson,
                req.Nickname),
            ct);

        return Ok(new { credentialId });
    }

    /// <summary>Get authentication options for navigator.credentials.get().</summary>
    [HttpPost("login/options")]
    [AllowAnonymous]
    public async Task<IActionResult> LoginOptions(CancellationToken ct)
    {
        var result = await _mediator.Send(new WebAuthnLoginOptionsCommand(), ct);
        return Ok(new { optionsJson = result.OptionsJson, challengeId = result.ChallengeId });
    }

    /// <summary>Complete authentication by verifying the assertion response.</summary>
    [HttpPost("login/complete")]
    [AllowAnonymous]
    public async Task<IActionResult> LoginComplete(
        [FromBody] LoginCompleteRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new WebAuthnLoginCompleteCommand(req.ChallengeId, req.AssertionResponseJson),
            ct);

        AuthCookieHelper.SetRefreshTokenCookie(HttpContext, _configuration, _environment, result);
        return Ok(AuthCookieHelper.ToClientLoginResult(result));
    }

    /// <summary>List all credentials for the authenticated user.</summary>
    [HttpGet("credentials")]
    [Authorize]
    public async Task<IActionResult> ListCredentials(CancellationToken ct)
    {
        var credentials = await _mediator.Send(
            new ListWebAuthnCredentialsQuery(CurrentUserId), ct);
        return Ok(credentials);
    }

    /// <summary>Delete a credential by ID.</summary>
    [HttpDelete("credentials/{id:guid}")]
    [Authorize]
    public async Task<IActionResult> DeleteCredential(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new DeleteWebAuthnCredentialCommand(CurrentUserId, id), ct);
        return NoContent();
    }

    /// <summary>Update a credential's nickname.</summary>
    [HttpPatch("credentials/{id:guid}")]
    [Authorize]
    public async Task<IActionResult> UpdateCredentialNickname(
        Guid id, [FromBody] UpdateNicknameRequest req, CancellationToken ct)
    {
        await _mediator.Send(
            new UpdateWebAuthnCredentialNicknameCommand(CurrentUserId, id, req.Nickname),
            ct);
        return NoContent();
    }
}

public record RegisterCompleteRequest(string ChallengeId, string AttestationResponseJson, string? Nickname);
public record LoginCompleteRequest(string ChallengeId, string AssertionResponseJson);
public record UpdateNicknameRequest(string? Nickname);
