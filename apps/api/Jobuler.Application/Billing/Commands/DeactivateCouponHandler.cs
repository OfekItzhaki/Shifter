using Jobuler.Infrastructure.Persistence;
using MediatR;

namespace Jobuler.Application.Billing.Commands;

public class DeactivateCouponHandler : IRequestHandler<DeactivateCouponCommand>
{
    private readonly AppDbContext _db;
    public DeactivateCouponHandler(AppDbContext db) => _db = db;

    public async Task Handle(DeactivateCouponCommand request, CancellationToken ct)
    {
        var user = await _db.Users.FindAsync(new object[] { request.UserId }, ct);
        if (user == null || !user.IsPlatformAdmin)
            throw new UnauthorizedAccessException("Platform admin access required.");

        var coupon = await _db.Coupons.FindAsync(new object[] { request.CouponId }, ct);
        if (coupon == null)
            throw new KeyNotFoundException("Coupon not found.");

        coupon.Deactivate();
        await _db.SaveChangesAsync(ct);
    }
}
