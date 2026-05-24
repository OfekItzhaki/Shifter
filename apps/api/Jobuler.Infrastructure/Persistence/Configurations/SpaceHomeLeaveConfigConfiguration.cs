using Jobuler.Domain.Groups;
using Jobuler.Domain.Spaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jobuler.Infrastructure.Persistence.Configurations;

public class SpaceHomeLeaveConfigConfiguration : IEntityTypeConfiguration<SpaceHomeLeaveConfig>
{
    public void Configure(EntityTypeBuilder<SpaceHomeLeaveConfig> builder)
    {
        builder.ToTable("space_home_leave_configs");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.SpaceId).HasColumnName("space_id");
        builder.Property(c => c.Mode).HasColumnName("mode")
            .HasConversion<int>();
        builder.Property(c => c.BalanceValue).HasColumnName("balance_value");
        builder.Property(c => c.BaseDays).HasColumnName("base_days");
        builder.Property(c => c.HomeDays).HasColumnName("home_days");
        builder.Property(c => c.MinPeopleAtBase).HasColumnName("min_people_at_base");
        builder.Property(c => c.MinRestHours).HasColumnName("min_rest_hours");
        builder.Property(c => c.EligibilityThresholdHours).HasColumnName("eligibility_threshold_hours");
        builder.Property(c => c.LeaveCapacity).HasColumnName("leave_capacity");
        builder.Property(c => c.LeaveDurationHours).HasColumnName("leave_duration_hours");
        builder.Property(c => c.EmergencyFreezeActive).HasColumnName("emergency_freeze_active");
        builder.Property(c => c.EmergencyUseForScheduling).HasColumnName("emergency_use_for_scheduling");
        builder.Property(c => c.FreezeStartedAt).HasColumnName("freeze_started_at");
        builder.Property(c => c.PreFreezeMode).HasColumnName("pre_freeze_mode")
            .HasConversion<int>();
        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(c => c.SpaceId).IsUnique();
    }
}
