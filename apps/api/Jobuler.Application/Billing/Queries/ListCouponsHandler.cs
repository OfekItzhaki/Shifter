using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Billing.Queries;

public class ListCouponsHandler : IRequestHandler<ListCouponsQuery, List<CouponDto>>
{
    private readonly AppDbContext _db;
    public ListCouponsHandler(AppDbContext db) => _db = db;

    public async Task<List<CouponDto>> Handle(ListCouponsQuery request, CancellationToken ct)
    {
        var user = await _db.Users.FindAsync(new object[] { request.UserId }, ct);
        if (user == null || !user.IsPlatformAdmin)
            throw new UnauthorizedAccessException("Platform admin access required.");

        var coupons = await _db.Coupons.OrderByDescending(c => c.CreatedAt).ToListAsync(ct);
        return coupons.Select(c => new CouponDto(
            c.Id, c.Code, c.DiscountPercent, c.MaxUses, c.CurrentUses,
            c.ValidFrom, c.ValidUntil, c.IsActive, c.Description
        )).ToList();
    }
}
