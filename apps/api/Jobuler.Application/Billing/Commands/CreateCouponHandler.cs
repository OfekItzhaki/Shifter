using Jobuler.Domain.Billing;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Billing.Commands;

public class CreateCouponHandler : IRequestHandler<CreateCouponCommand, CreateCouponResult>
{
    private readonly AppDbContext _db;
    public CreateCouponHandler(AppDbContext db) => _db = db;

    public async Task<CreateCouponResult> Handle(CreateCouponCommand request, CancellationToken ct)
    {
        var user = await _db.Users.FindAsync(new object[] { request.UserId }, ct);
        if (user == null || !user.IsPlatformAdmin)
            throw new UnauthorizedAccessException("Platform admin access required.");

        if (string.IsNullOrWhiteSpace(request.Code) || request.DiscountPercent < 1 || request.DiscountPercent > 100)
            throw new InvalidOperationException("Invalid coupon data.");

        var exists = await _db.Coupons.AnyAsync(c => c.Code == request.Code.ToUpperInvariant().Trim(), ct);
        if (exists)
            throw new InvalidOperationException("Coupon code already exists.");

        var coupon = Coupon.Create(request.Code, request.DiscountPercent, request.MaxUses, request.ValidUntil, request.Description);
        _db.Coupons.Add(coupon);
        await _db.SaveChangesAsync(ct);

        return new CreateCouponResult(coupon.Id, coupon.Code, coupon.DiscountPercent);
    }
}
