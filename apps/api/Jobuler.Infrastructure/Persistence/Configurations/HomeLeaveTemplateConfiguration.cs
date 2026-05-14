using Jobuler.Domain.Groups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jobuler.Infrastructure.Persistence.Configurations;

public class HomeLeaveTemplateConfiguration : IEntityTypeConfiguration<HomeLeaveTemplate>
{
    public void Configure(EntityTypeBuilder<HomeLeaveTemplate> builder)
    {
        builder.ToTable("home_leave_templates");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.SpaceId).HasColumnName("space_id");
        builder.Property(t => t.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(t => t.MinRestHours).HasColumnName("min_rest_hours");
        builder.Property(t => t.EligibilityThresholdHours).HasColumnName("eligibility_threshold_hours");
        builder.Property(t => t.LeaveCapacity).HasColumnName("leave_capacity");
        builder.Property(t => t.LeaveDurationHours).HasColumnName("leave_duration_hours");
        builder.Property(t => t.CreatedAt).HasColumnName("created_at");

        // Composite unique index on (space_id, name) (Requirement 12.3)
        builder.HasIndex(t => new { t.SpaceId, t.Name }).IsUnique();
    }
}
