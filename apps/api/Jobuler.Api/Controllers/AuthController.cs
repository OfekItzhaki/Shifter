using FluentValidation;
using Jobuler.Application.Auth.Commands;
using Jobuler.Application.Auth.Queries;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Jobuler.Api.Controllers;

[ApiController]
[Route("auth")]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator) => _mediator = mediator;

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>Register a new user account.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req, CancellationToken ct)
    {
        var userId = await _mediator.Send(
            new RegisterCommand(req.Email ?? "", req.DisplayName, req.Password, req.PreferredLocale ?? "he",
                req.PhoneNumber, req.ProfileImageUrl, req.Birthday), ct);
        return CreatedAtAction(nameof(Register), new { userId });
    }

    /// <summary>Get current user's profile.</summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetMe([FromServices] AppDbContext db, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == CurrentUserId, ct);
        if (user is null) return NotFound();
        return Ok(new {
            id = user.Id,
            email = user.Email,
            displayName = user.DisplayName,
            phoneNumber = user.PhoneNumber,
            profileImageUrl = user.ProfileImageUrl,
            birthday = user.Birthday,
            createdAt = user.CreatedAt,
            emailVerified = user.EmailVerified,
            isPlatformAdmin = user.IsPlatformAdmin
        });
    }

    /// <summary>Export all user data (GDPR compliance).</summary>
    [HttpGet("me/export")]
    [Authorize]
    public async Task<IActionResult> ExportMyData(CancellationToken ct)
    {
        var result = await _mediator.Send(new ExportMyDataQuery(CurrentUserId), ct);
        var json = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        });
        return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", $"shifter-data-export-{DateTime.UtcNow:yyyy-MM-dd}.json");
    }

    /// <summary>Update current user's profile.</summary>
    [HttpPut("me")]
    [Authorize]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateMeRequest req, [FromServices] AppDbContext db, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == CurrentUserId, ct);
        if (user is null) return NotFound();
        user.UpdateProfileFull(req.DisplayName, req.ProfileImageUrl, req.PhoneNumber, req.Birthday);

        // Sync profile image and display name to linked Person records
        var linkedPeople = await db.People
            .Where(p => p.LinkedUserId == CurrentUserId)
            .ToListAsync(ct);
        foreach (var person in linkedPeople)
        {
            person.UpdateFull(
                string.IsNullOrWhiteSpace(req.DisplayName) ? person.FullName : req.DisplayName,
                req.DisplayName ?? person.DisplayName,
                req.ProfileImageUrl ?? person.ProfileImageUrl,
                req.PhoneNumber ?? person.PhoneNumber,
                person.Birthday);
        }

        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Login and receive access + refresh tokens.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(new LoginCommand(req.ResolvedIdentifier, req.Password), ct);
        return Ok(result);
    }

    /// <summary>Exchange a valid refresh token for a new token pair.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [DisableRateLimiting]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(new RefreshTokenCommand(req.RefreshToken), ct);
        return Ok(result);
    }

    /// <summary>Revoke the current user's refresh token (logout).</summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest req, CancellationToken ct)
    {
        await _mediator.Send(new RevokeTokenCommand(req.RefreshToken), ct);
        return NoContent();
    }

    /// <summary>Request a password reset token. Always returns 200 to prevent user enumeration.</summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequest req, CancellationToken ct)
    {
        await _mediator.Send(new ForgotPasswordCommand(req.Email), ct);
        return Ok();
    }

    /// <summary>Reset password using a valid reset token.</summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest req, CancellationToken ct)
    {
        await _mediator.Send(new ResetPasswordCommand(req.Token, req.NewPassword), ct);
        return NoContent();
    }

    /// <summary>Verify email using a token from the verification email.</summary>
    [HttpPost("verify-email")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest req, CancellationToken ct)
    {
        await _mediator.Send(new VerifyEmailCommand(req.Token), ct);
        return NoContent();
    }

    /// <summary>Resend verification email to the current user.</summary>
    [HttpPost("resend-verification")]
    [Authorize]
    public async Task<IActionResult> ResendVerification(CancellationToken ct)
    {
        await _mediator.Send(new ResendVerificationCommand(CurrentUserId), ct);
        return NoContent();
    }

    /// <summary>Re-authenticate the current user before entering an elevated privilege mode.</summary>
    [HttpPost("re-authenticate")]
    [Authorize]
    public async Task<IActionResult> ReAuthenticate([FromBody] ReAuthenticateRequest req, CancellationToken ct)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

        var result = await _mediator.Send(new ReAuthenticateCommand(
            CurrentUserId,
            req.Password,
            req.WebAuthnChallengeId,
            req.WebAuthnAssertionJson,
            req.SpaceId,
            ipAddress), ct);

        if (result.Success)
            return Ok(new { success = true });

        return Unauthorized(new { error = "Authentication failed." });
    }

    /// <summary>Record a session timeout event for audit purposes.</summary>
    [HttpPost("session-timeout-event")]
    [Authorize]
    public async Task<IActionResult> SessionTimeoutEvent([FromBody] SessionTimeoutEventRequest req, CancellationToken ct)
    {
        await _mediator.Send(new RecordSessionTimeoutCommand(CurrentUserId, req.SpaceId, req.Mode), ct);
        return NoContent();
    }

    /// <summary>Delete the current user's account and all associated data.</summary>
    [HttpDelete("me")]
    [Authorize]
    public async Task<IActionResult> DeleteAccount([FromServices] AppDbContext db, CancellationToken ct)
    {
        var userId = CurrentUserId;

        // Remove all linked person records
        var linkedPeople = await db.People
            .Where(p => p.LinkedUserId == userId)
            .ToListAsync(ct);
        db.People.RemoveRange(linkedPeople);

        // Remove refresh tokens
        var tokens = await db.RefreshTokens
            .Where(t => t.UserId == userId)
            .ToListAsync(ct);
        db.RefreshTokens.RemoveRange(tokens);

        // Remove notifications
        var notifications = await db.Notifications
            .Where(n => n.UserId == userId)
            .ToListAsync(ct);
        db.Notifications.RemoveRange(notifications);

        // Remove the user
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user != null) db.Users.Remove(user);

        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}

public record RegisterRequest(string? Email, string DisplayName, string Password, string? PreferredLocale, string? PhoneNumber, string? ProfileImageUrl = null, DateOnly? Birthday = null);
public record LoginRequest(string? Email, string? Identifier, string Password)
{
    /// <summary>Resolves the login identifier — supports both "email" (legacy) and "identifier" (new) fields.</summary>
    public string ResolvedIdentifier => Identifier ?? Email ?? "";
}
public record RefreshRequest(string RefreshToken);
public record ForgotPasswordRequest(string Email);
public record ResetPasswordRequest(string Token, string NewPassword);
public record UpdateMeRequest(string DisplayName, string? PhoneNumber, string? ProfileImageUrl, DateOnly? Birthday);
public record VerifyEmailRequest(string Token);
public record SessionTimeoutEventRequest(Guid? SpaceId, string Mode);
public record ReAuthenticateRequest(string? Password, string? WebAuthnChallengeId, string? WebAuthnAssertionJson, Guid? SpaceId);
