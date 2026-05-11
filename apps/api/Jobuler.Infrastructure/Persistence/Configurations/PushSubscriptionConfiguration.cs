using Jobuler.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jobuler.Infrastructure.Persistence.Configurations;

public class PushSubscriptionConfiguration : IEntityTypeConfiguration<PushSubscription>
{
    public void Configure(EntityTypeBuilder<PushSubscription> builder)
    {
        builder.ToTable("push_subscriptions");
        builder.HasKey(ps => ps.Id);
        builder.Property(ps => ps.Id).HasColumnName("id");
        builder.Property(ps => ps.SpaceId).HasColumnName("space_id").IsRequired();
        builder.Property(ps => ps.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(ps => ps.Endpoint).HasColumnName("endpoint").IsRequired();
        builder.Property(ps => ps.P256dh).HasColumnName("p256dh").IsRequired();
        builder.Property(ps => ps.Auth).HasColumnName("auth").IsRequired();
        builder.Property(ps => ps.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(ps => new { ps.UserId, ps.SpaceId, ps.Endpoint })
            .IsUnique()
            .HasDatabaseName("uq_push_sub_user_space_endpoint");

        builder.HasIndex(ps => new { ps.UserId, ps.SpaceId })
            .HasDatabaseName("ix_push_subscriptions_user_space");
    }
}
