using FluentValidation;
using Jobuler.Application.Auth.Commands;
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
            new RegisterCommand(req.Email, req.DisplayName, req.Password, req.PreferredLocale ?? "he",
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
            createdAt = user.CreatedAt
        });
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
        var result = await _mediator.Send(new LoginCommand(req.Email, req.Password), ct);
        return Ok(result);
    }

    /// <summary>Exchange a valid refresh token for a new token pair.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
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
}

public record RegisterRequest(string Email, string DisplayName, string Password, string? PreferredLocale, string? PhoneNumber, string? ProfileImageUrl = null, DateOnly? Birthday = null);
public record LoginRequest(string Email, string Password);
public record RefreshRequest(string RefreshToken);
public record ForgotPasswordRequest(string Email);
public record ResetPasswordRequest(string Token, string NewPassword);
public record UpdateMeRequest(string DisplayName, string? PhoneNumber, string? ProfileImageUrl, DateOnly? Birthday);
