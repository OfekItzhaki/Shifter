using Jobuler.Domain.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jobuler.Infrastructure.Persistence.Configurations;

public class CumulativeRecordConfiguration : IEntityTypeConfiguration<CumulativeRecord>
{
    public void Configure(EntityTypeBuilder<CumulativeRecord> builder)
    {
        builder.ToTable("cumulative_records");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.SpaceId).HasColumnName("space_id");
        builder.Property(r => r.GroupId).HasColumnName("group_id");
        builder.Property(r => r.PersonId).HasColumnName("person_id");
        builder.Property(r => r.PeriodId).HasColumnName("period_id");

        // Consecutive hours tracking
        builder.Property(r => r.ConsecutiveHoursAtBase).HasColumnName("consecutive_hours_at_base")
            .HasColumnType("numeric(10,2)");
        builder.Property(r => r.LastHomeLeaveEnd).HasColumnName("last_home_leave_end");

        // Multi-window counters: total assignments
        builder.Property(r => r.TotalAssignments7d).HasColumnName("total_assignments_7d");
        builder.Property(r => r.TotalAssignments14d).HasColumnName("total_assignments_14d");
        builder.Property(r => r.TotalAssignments30d).HasColumnName("total_assignments_30d");
        builder.Property(r => r.TotalAssignments90d).HasColumnName("total_assignments_90d");
        builder.Property(r => r.TotalAssignmentsPeriod).HasColumnName("total_assignments_period");

        // Multi-window counters: hard tasks
        builder.Property(r => r.HardTasks7d).HasColumnName("hard_tasks_7d");
        builder.Property(r => r.HardTasks14d).HasColumnName("hard_tasks_14d");
        builder.Property(r => r.HardTasks30d).HasColumnName("hard_tasks_30d");
        builder.Property(r => r.HardTasks90d).HasColumnName("hard_tasks_90d");
        builder.Property(r => r.HardTasksPeriod).HasColumnName("hard_tasks_period");

        // Multi-window counters: night missions
        builder.Property(r => r.NightMissions7d).HasColumnName("night_missions_7d");
        builder.Property(r => r.NightMissions14d).HasColumnName("night_missions_14d");
        builder.Property(r => r.NightMissions30d).HasColumnName("night_missions_30d");
        builder.Property(r => r.NightMissions90d).HasColumnName("night_missions_90d");
        builder.Property(r => r.NightMissionsPeriod).HasColumnName("night_missions_period");

        // Generic task-type counts (JSONB)
        builder.Property(r => r.TaskTypeCountsJson).HasColumnName("task_type_counts")
            .HasColumnType("jsonb")
            .HasDefaultValue("{}");

        builder.Property(r => r.TotalHoursAssignedPeriod).HasColumnName("total_hours_assigned_period")
            .HasColumnType("numeric(10,2)");
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");

        // cumulative_records has no created_at column — ignore the base Entity property
        builder.Ignore(r => r.CreatedAt);

        // Unique constraint
        builder.HasIndex(r => new { r.SpaceId, r.GroupId, r.PersonId, r.PeriodId }).IsUnique();

        // Indexes
        builder.HasIndex(r => new { r.SpaceId, r.GroupId, r.PeriodId })
            .HasDatabaseName("idx_cumulative_records_lookup");
        builder.HasIndex(r => new { r.PersonId, r.PeriodId })
            .HasDatabaseName("idx_cumulative_records_person");
    }
}
