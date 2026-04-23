using Jobuler.Domain.People;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.People.Commands;

public record AddQualificationCommand(
    Guid SpaceId, Guid PersonId, string Qualification,
    DateOnly? IssuedAt, DateOnly? ExpiresAt) : IRequest<Guid>;

public class AddQualificationCommandHandler : IRequestHandler<AddQualificationCommand, Guid>
{
    private readonly AppDbContext _db;
    public AddQualificationCommandHandler(AppDbContext db) => _db = db;

    public async Task<Guid> Handle(AddQualificationCommand req, CancellationToken ct)
    {
        var personExists = await _db.People.AnyAsync(
            p => p.Id == req.PersonId && p.SpaceId == req.SpaceId, ct);
        if (!personExists) throw new KeyNotFoundException("Person not found.");

        var qual = PersonQualification.Create(
            req.SpaceId, req.PersonId, req.Qualification,
            req.IssuedAt, req.ExpiresAt);

        _db.PersonQualifications.Add(qual);
        await _db.SaveChangesAsync(ct);
        return qual.Id;
    }
}
