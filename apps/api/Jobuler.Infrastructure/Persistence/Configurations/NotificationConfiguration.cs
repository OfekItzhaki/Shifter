using Jobuler.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jobuler.Infrastructure.Persistence.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).HasColumnName("id");
        builder.Property(n => n.SpaceId).HasColumnName("space_id").IsRequired();
        builder.Property(n => n.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(n => n.EventType).HasColumnName("event_type").HasMaxLength(100).IsRequired();
        builder.Property(n => n.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
        builder.Property(n => n.Body).HasColumnName("body").IsRequired();
        builder.Property(n => n.MetadataJson).HasColumnName("metadata_json").HasColumnType("jsonb");
        builder.Property(n => n.IsRead).HasColumnName("is_read");
        builder.Property(n => n.CreatedAt).HasColumnName("created_at");
        builder.Property(n => n.ReadAt).HasColumnName("read_at");
    }
}
