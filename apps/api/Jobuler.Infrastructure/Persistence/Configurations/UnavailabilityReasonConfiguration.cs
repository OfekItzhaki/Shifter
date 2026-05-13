using Jobuler.Domain.Spaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jobuler.Infrastructure.Persistence.Configurations;

public class UnavailabilityReasonConfiguration : IEntityTypeConfiguration<UnavailabilityReason>
{
    public void Configure(EntityTypeBuilder<UnavailabilityReason> builder)
    {
        builder.ToTable("unavailability_reasons");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.SpaceId).HasColumnName("space_id");
        builder.Property(r => r.DisplayName).HasColumnName("display_name").HasMaxLength(100).IsRequired();
        builder.Property(r => r.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.Property(r => r.IsActive).HasColumnName("is_active");
        builder.Property(r => r.CreatedAt).HasColumnName("created_at");
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(r => new { r.SpaceId, r.IsActive });
    }
}
