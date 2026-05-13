using FluentValidation;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Auth.Commands;

/// <summary>
/// Updates the nickname of a WebAuthn credential. Validates ownership and nickname length.
/// </summary>
public record UpdateWebAuthnCredentialNicknameCommand(
    Guid UserId,
    Guid CredentialId,
    string? NewNickname) : IRequest;

public class UpdateWebAuthnCredentialNicknameCommandValidator
    : AbstractValidator<UpdateWebAuthnCredentialNicknameCommand>
{
    public UpdateWebAuthnCredentialNicknameCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.CredentialId).NotEmpty();
        RuleFor(x => x.NewNickname)
            .MaximumLength(100)
            .When(x => x.NewNickname is not null);
    }
}

public class UpdateWebAuthnCredentialNicknameCommandHandler
    : IRequestHandler<UpdateWebAuthnCredentialNicknameCommand>
{
    private readonly AppDbContext _db;

    public UpdateWebAuthnCredentialNicknameCommandHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task Handle(UpdateWebAuthnCredentialNicknameCommand request, CancellationToken ct)
    {
        var credential = await _db.WebAuthnCredentials
            .FirstOrDefaultAsync(c => c.Id == request.CredentialId, ct)
            ?? throw new KeyNotFoundException("Credential not found.");

        // Ownership check
        if (credential.UserId != request.UserId)
            throw new UnauthorizedAccessException("Cannot modify a credential that belongs to another user.");

        // Update via domain method (validates nickname length)
        credential.UpdateNickname(request.NewNickname);
        await _db.SaveChangesAsync(ct);
    }
}
