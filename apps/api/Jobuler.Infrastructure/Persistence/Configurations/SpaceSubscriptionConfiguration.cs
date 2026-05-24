using Jobuler.Domain.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jobuler.Infrastructure.Persistence.Configurations;

public class SpaceSubscriptionConfiguration : IEntityTypeConfiguration<SpaceSubscription>
{
    public void Configure(EntityTypeBuilder<SpaceSubscription> builder)
    {
        builder.ToTable("space_subscriptions");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.SpaceId).HasColumnName("space_id").IsRequired();
        builder.Property(s => s.TierId).HasColumnName("tier_id").IsRequired();
        builder.Property(s => s.Status).HasColumnName("status").HasConversion<string>()
            .IsRequired();
        builder.Property(s => s.LemonSqueezySubscriptionId).HasColumnName("lemonsqueezy_subscription_id");
        builder.Property(s => s.LemonSqueezyCustomerId).HasColumnName("lemonsqueezy_customer_id");
        builder.Property(s => s.TrialStartsAt).HasColumnName("trial_starts_at").IsRequired();
        builder.Property(s => s.TrialEndsAt).HasColumnName("trial_ends_at").IsRequired();
        builder.Property(s => s.CurrentPeriodStart).HasColumnName("current_period_start");
        builder.Property(s => s.CurrentPeriodEnd).HasColumnName("current_period_end");
        builder.Property(s => s.PeakMemberCount).HasColumnName("peak_member_count").IsRequired();
        builder.Property(s => s.CanceledAt).HasColumnName("canceled_at");
        builder.Property(s => s.AutoRenew).HasColumnName("auto_renew").IsRequired();
        builder.Property(s => s.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(s => s.SpaceId).IsUnique()
            .HasDatabaseName("uq_space_subscriptions_space_id");
        builder.HasIndex(s => s.Status)
            .HasDatabaseName("idx_space_subscriptions_status");
    }
}
