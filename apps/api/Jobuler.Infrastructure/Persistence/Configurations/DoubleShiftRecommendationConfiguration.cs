using Jobuler.Domain.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jobuler.Infrastructure.Persistence.Configurations;

public class DoubleShiftRecommendationConfiguration : IEntityTypeConfiguration<DoubleShiftRecommendation>
{
    public void Configure(EntityTypeBuilder<DoubleShiftRecommendation> builder)
    {
        builder.ToTable("double_shift_recommendations");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.SpaceId).HasColumnName("space_id");
        builder.Property(r => r.GroupId).HasColumnName("group_id");
        builder.Property(r => r.ScheduleRunId).HasColumnName("schedule_run_id");
        builder.Property(r => r.GroupTaskId).HasColumnName("group_task_id");
        builder.Property(r => r.TaskName).HasColumnName("task_name").HasMaxLength(200).IsRequired();
        builder.Property(r => r.Status).HasColumnName("status")
            .HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(r => r.AdditionalSlotsCovered).HasColumnName("additional_slots_covered");
        builder.Property(r => r.AffectedDateStart).HasColumnName("affected_date_start");
        builder.Property(r => r.AffectedDateEnd).HasColumnName("affected_date_end");
        builder.Property(r => r.TotalUncoveredSlotsInRun).HasColumnName("total_uncovered_slots_in_run");
        builder.Property(r => r.DismissedAt).HasColumnName("dismissed_at");
        builder.Property(r => r.DismissedByUserId).HasColumnName("dismissed_by_user_id");
        builder.Property(r => r.ResolvedAt).HasColumnName("resolved_at");
        builder.Property(r => r.ClearedAt).HasColumnName("cleared_at");
        builder.Property(r => r.CreatedAt).HasColumnName("created_at");

        // Unique constraint for upsert pattern: one recommendation per (space, run, task)
        builder.HasIndex(r => new { r.SpaceId, r.ScheduleRunId, r.GroupTaskId })
            .IsUnique()
            .HasDatabaseName("uq_dsr_space_run_task");

        builder.HasIndex(r => new { r.SpaceId, r.GroupId, r.Status })
            .HasDatabaseName("ix_dsr_space_group_status");
        builder.HasIndex(r => new { r.SpaceId, r.ScheduleRunId })
            .HasDatabaseName("ix_dsr_space_run");
        builder.HasIndex(r => new { r.SpaceId, r.GroupTaskId, r.Status })
            .HasDatabaseName("ix_dsr_space_task_status");
        builder.HasIndex(r => r.CreatedAt)
            .HasDatabaseName("ix_dsr_created_at");
    }
}
