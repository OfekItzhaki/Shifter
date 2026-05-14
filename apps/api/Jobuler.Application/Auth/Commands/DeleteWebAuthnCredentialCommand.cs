using FluentValidation;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Auth.Commands;

/// <summary>
/// Deletes a WebAuthn credential. Validates that the credential belongs to the requesting user.
/// </summary>
public record DeleteWebAuthnCredentialCommand(Guid UserId, Guid CredentialId) : IRequest;

public class DeleteWebAuthnCredentialCommandValidator : AbstractValidator<DeleteWebAuthnCredentialCommand>
{
    public DeleteWebAuthnCredentialCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.CredentialId).NotEmpty();
    }
}

public class DeleteWebAuthnCredentialCommandHandler
    : IRequestHandler<DeleteWebAuthnCredentialCommand>
{
    private readonly AppDbContext _db;

    public DeleteWebAuthnCredentialCommandHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task Handle(DeleteWebAuthnCredentialCommand request, CancellationToken ct)
    {
        var credential = await _db.WebAuthnCredentials
            .FirstOrDefaultAsync(c => c.Id == request.CredentialId, ct)
            ?? throw new KeyNotFoundException("Credential not found.");

        // Ownership check — reject cross-user deletion
        if (credential.UserId != request.UserId)
            throw new UnauthorizedAccessException("Cannot delete a credential that belongs to another user.");

        _db.WebAuthnCredentials.Remove(credential);
        await _db.SaveChangesAsync(ct);
    }
}
