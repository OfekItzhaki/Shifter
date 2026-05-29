using Jobuler.Domain.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jobuler.Infrastructure.Persistence.Configurations;

public class GroupSubscriptionConfiguration : IEntityTypeConfiguration<GroupSubscription>
{
    public void Configure(EntityTypeBuilder<GroupSubscription> builder)
    {
        builder.ToTable("group_subscriptions");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.SpaceId).HasColumnName("space_id").IsRequired();
        builder.Property(s => s.GroupId).HasColumnName("group_id").IsRequired();
        builder.Property(s => s.TierId).HasColumnName("tier_id").IsRequired();
        builder.Property(s => s.Status).HasColumnName("status").HasConversion<string>()
            .IsRequired();
        builder.Property(s => s.LemonSqueezySubscriptionId).HasColumnName("lemonsqueezy_subscription_id");
        builder.Property(s => s.LemonSqueezyCustomerId).HasColumnName("lemonsqueezy_customer_id");
        builder.Property(s => s.TrialEndsAt).HasColumnName("trial_ends_at");
        builder.Property(s => s.CurrentPeriodStart).HasColumnName("current_period_start");
        builder.Property(s => s.CurrentPeriodEnd).HasColumnName("current_period_end");
        builder.Property(s => s.PeakMemberCount).HasColumnName("peak_member_count");
        builder.Property(s => s.CouponCode).HasColumnName("coupon_code");
        builder.Property(s => s.DiscountPercent).HasColumnName("discount_percent");
        builder.Property(s => s.CanceledAt).HasColumnName("canceled_at");
        builder.Property(s => s.CreatedAt).HasColumnName("created_at");
        builder.Property<DateTime>("UpdatedAt").HasColumnName("updated_at");
    }
}

public class WebhookEventLogConfiguration : IEntityTypeConfiguration<WebhookEventLog>
{
    public void Configure(EntityTypeBuilder<WebhookEventLog> builder)
    {
        builder.ToTable("webhook_event_logs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.EventId).HasColumnName("event_id").HasMaxLength(255).IsRequired();
        builder.Property(e => e.EventType).HasColumnName("event_type").HasMaxLength(100).IsRequired();
        builder.Property(e => e.ProcessedAt).HasColumnName("processed_at").IsRequired();
        builder.Property(e => e.ProcessedSuccessfully).HasColumnName("processed_successfully").HasDefaultValue(false);
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(e => e.EventId).IsUnique();
        builder.HasIndex(e => e.ProcessedAt);
    }
}


