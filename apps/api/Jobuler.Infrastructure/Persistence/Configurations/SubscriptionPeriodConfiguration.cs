using Jobuler.Domain.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jobuler.Infrastructure.Persistence.Configurations;

public class SubscriptionPeriodConfiguration : IEntityTypeConfiguration<SubscriptionPeriod>
{
    public void Configure(EntityTypeBuilder<SubscriptionPeriod> builder)
    {
        builder.ToTable("subscription_periods");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.SpaceId).HasColumnName("space_id");
        builder.Property(p => p.GroupId).HasColumnName("group_id");
        builder.Property(p => p.Status).HasColumnName("status");
        builder.Property(p => p.StartsAt).HasColumnName("starts_at");
        builder.Property(p => p.EndsAt).HasColumnName("ends_at");
        builder.Property(p => p.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(p => new { p.SpaceId, p.GroupId })
            .HasDatabaseName("idx_subscription_periods_group");

        builder.HasIndex(p => new { p.GroupId, p.Status })
            .HasFilter("status = 'active'")
            .HasDatabaseName("idx_subscription_periods_active");
    }
}
