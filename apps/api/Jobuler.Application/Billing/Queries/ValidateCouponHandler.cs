using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Billing.Queries;

public class ValidateCouponHandler : IRequestHandler<ValidateCouponQuery, CouponValidationResult>
{
    private readonly AppDbContext _db;
    public ValidateCouponHandler(AppDbContext db) => _db = db;

    public async Task<CouponValidationResult> Handle(ValidateCouponQuery request, CancellationToken ct)
    {
        var coupon = await _db.Coupons
            .FirstOrDefaultAsync(c => c.Code == request.Code.ToUpperInvariant().Trim(), ct);

        if (coupon == null || !coupon.IsValid)
            return new CouponValidationResult(false, 0);

        return new CouponValidationResult(true, coupon.DiscountPercent);
    }
}
