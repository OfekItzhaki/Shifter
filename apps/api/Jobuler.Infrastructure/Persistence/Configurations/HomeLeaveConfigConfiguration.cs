using Jobuler.Domain.Groups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jobuler.Infrastructure.Persistence.Configurations;

public class HomeLeaveConfigConfiguration : IEntityTypeConfiguration<HomeLeaveConfig>
{
    public void Configure(EntityTypeBuilder<HomeLeaveConfig> builder)
    {
        builder.ToTable("home_leave_configs");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.SpaceId).HasColumnName("space_id");
        builder.Property(c => c.GroupId).HasColumnName("group_id");
        builder.Property(c => c.MinRestHours).HasColumnName("min_rest_hours");
        builder.Property(c => c.EligibilityThresholdHours).HasColumnName("eligibility_threshold_hours");
        builder.Property(c => c.LeaveCapacity).HasColumnName("leave_capacity");
        builder.Property(c => c.LeaveDurationHours).HasColumnName("leave_duration_hours");
        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");

        // One-to-one: unique constraint on group_id (Requirement 12.7)
        builder.HasIndex(c => c.GroupId).IsUnique();
    }
}
