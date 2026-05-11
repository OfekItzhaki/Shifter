using Jobuler.Domain.Common;

namespace Jobuler.Domain.Billing;

public class Coupon : Entity
{
    public string Code { get; private set; } = default!;
    public int DiscountPercent { get; private set; }
    public int? MaxUses { get; private set; }
    public int CurrentUses { get; private set; }
    public DateTime ValidFrom { get; private set; }
    public DateTime? ValidUntil { get; private set; }
    public bool IsActive { get; private set; } = true;
    public string? Description { get; private set; }

    private Coupon() { }

    public static Coupon Create(string code, int discountPercent, int? maxUses = null,
        DateTime? validUntil = null, string? description = null) =>
        new()
        {
            Code = code.ToUpperInvariant().Trim(),
            DiscountPercent = discountPercent,
            MaxUses = maxUses,
            ValidFrom = DateTime.UtcNow,
            ValidUntil = validUntil,
            Description = description,
        };

    public bool IsValid =>
        IsActive
        && DateTime.UtcNow >= ValidFrom
        && (ValidUntil == null || DateTime.UtcNow <= ValidUntil)
        && (MaxUses == null || CurrentUses < MaxUses);

    public void Use() => CurrentUses++;

    public void Deactivate() => IsActive = false;
}
