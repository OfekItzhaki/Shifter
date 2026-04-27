using Jobuler.Domain.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Jobuler.Infrastructure.Persistence.Configurations;

public class ScheduleRunConfiguration : IEntityTypeConfiguration<ScheduleRun>
{
    public void Configure(EntityTypeBuilder<ScheduleRun> builder)
    {
        builder.ToTable("schedule_runs");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.SpaceId).HasColumnName("space_id");
        builder.Property(r => r.TriggerType).HasColumnName("trigger_type")
            .HasConversion(v => v.ToString().ToLower(), v => Enum.Parse<ScheduleRunTrigger>(v, true));
        builder.Property(r => r.BaselineVersionId).HasColumnName("baseline_version_id");
        builder.Property(r => r.RequestedByUserId).HasColumnName("requested_by_user_id");
        builder.Property(r => r.Status).HasColumnName("status")
            .HasConversion(v => v.ToString().ToLower(), v => Enum.Parse<ScheduleRunStatus>(v, true));
        builder.Property(r => r.SolverInputHash).HasColumnName("solver_input_hash");
        builder.Property(r => r.StartedAt).HasColumnName("started_at");
        builder.Property(r => r.FinishedAt).HasColumnName("finished_at");
        builder.Property(r => r.DurationMs).HasColumnName("duration_ms");
        builder.Property(r => r.ResultSummaryJson).HasColumnName("result_summary_json")
            .HasColumnType("jsonb");
        builder.Property(r => r.ErrorSummary).HasColumnName("error_summary");
        builder.Property(r => r.CreatedAt).HasColumnName("created_at");
    }
}

public class ScheduleVersionConfiguration : IEntityTypeConfiguration<ScheduleVersion>
{
    public void Configure(EntityTypeBuilder<ScheduleVersion> builder)
    {
        builder.ToTable("schedule_versions");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).HasColumnName("id");
        builder.Property(v => v.SpaceId).HasColumnName("space_id");
        builder.Property(v => v.VersionNumber).HasColumnName("version_number");
        var statusConverter = new ValueConverter<ScheduleVersionStatus, string>(
            v => v == ScheduleVersionStatus.RolledBack ? "rolled_back"
               : v == ScheduleVersionStatus.Discarded  ? "discarded"
               : v.ToString().ToLower(),
            v => v == "rolled_back" ? ScheduleVersionStatus.RolledBack
               : v == "discarded"  ? ScheduleVersionStatus.Discarded
               : Enum.Parse<ScheduleVersionStatus>(v, true));
        builder.Property(v => v.Status).HasColumnName("status").HasConversion(statusConverter);
        builder.Property(v => v.BaselineVersionId).HasColumnName("baseline_version_id");
        builder.Property(v => v.SourceRunId).HasColumnName("source_run_id");
        builder.Property(v => v.RollbackSourceVersionId).HasColumnName("rollback_source_version_id");
        builder.Property(v => v.CreatedByUserId).HasColumnName("created_by_user_id");
        builder.Property(v => v.PublishedByUserId).HasColumnName("published_by_user_id");
        builder.Property(v => v.PublishedAt).HasColumnName("published_at");
        builder.Property(v => v.SummaryJson).HasColumnName("summary_json").HasColumnType("jsonb");
        builder.Property(v => v.CreatedAt).HasColumnName("created_at");
        builder.HasIndex(v => new { v.SpaceId, v.VersionNumber }).IsUnique();
    }
}

public class AssignmentConfiguration : IEntityTypeConfiguration<Assignment>
{
    public void Configure(EntityTypeBuilder<Assignment> builder)
    {
        builder.ToTable("assignments");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.SpaceId).HasColumnName("space_id");
        builder.Property(a => a.ScheduleVersionId).HasColumnName("schedule_version_id");
        builder.Property(a => a.TaskSlotId).HasColumnName("task_slot_id");
        builder.Property(a => a.PersonId).HasColumnName("person_id");
        builder.Property(a => a.Source).HasColumnName("assignment_source")
            .HasConversion(v => v.ToString().ToLower(), v => Enum.Parse<AssignmentSource>(v, true));
        builder.Property(a => a.ChangeReasonSummary).HasColumnName("change_reason_summary");
        builder.Property(a => a.CreatedAt).HasColumnName("created_at");
        builder.HasIndex(a => new { a.ScheduleVersionId, a.TaskSlotId, a.PersonId }).IsUnique();
    }
}

public class AssignmentChangeSummaryConfiguration : IEntityTypeConfiguration<AssignmentChangeSummary>
{
    public void Configure(EntityTypeBuilder<AssignmentChangeSummary> builder)
    {
        builder.ToTable("assignment_change_summaries");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.SpaceId).HasColumnName("space_id");
        builder.Property(s => s.VersionId).HasColumnName("version_id");
        builder.Property(s => s.BaselineVersionId).HasColumnName("baseline_version_id");
        builder.Property(s => s.AddedCount).HasColumnName("added_count");
        builder.Property(s => s.RemovedCount).HasColumnName("removed_count");
        builder.Property(s => s.ChangedCount).HasColumnName("changed_count");
        builder.Property(s => s.StabilityScore).HasColumnName("stability_score");
        builder.Property(s => s.DiffJson).HasColumnName("diff_json").HasColumnType("jsonb");
        builder.Property(s => s.ComputedAt).HasColumnName("computed_at");
        // assignment_change_summaries has no created_at column
        builder.Ignore(s => s.CreatedAt);
    }
}

public class FairnessCounterConfiguration : IEntityTypeConfiguration<FairnessCounter>
{
    public void Configure(EntityTypeBuilder<FairnessCounter> builder)
    {
        builder.ToTable("fairness_counters");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasColumnName("id");
        builder.Property(f => f.SpaceId).HasColumnName("space_id");
        builder.Property(f => f.PersonId).HasColumnName("person_id");
        builder.Property(f => f.AsOfDate).HasColumnName("as_of_date");
        builder.Property(f => f.TotalAssignments7d).HasColumnName("total_assignments_7d");
        builder.Property(f => f.TotalAssignments14d).HasColumnName("total_assignments_14d");
        builder.Property(f => f.TotalAssignments30d).HasColumnName("total_assignments_30d");
        builder.Property(f => f.HatedTasks7d).HasColumnName("hated_tasks_7d");
        builder.Property(f => f.HatedTasks14d).HasColumnName("hated_tasks_14d");
        builder.Property(f => f.DislikedHatedScore7d).HasColumnName("disliked_hated_score_7d");
        builder.Property(f => f.KitchenCount7d).HasColumnName("kitchen_count_7d");
        builder.Property(f => f.NightMissions7d).HasColumnName("night_missions_7d");
        builder.Property(f => f.ConsecutiveBurdenCount).HasColumnName("consecutive_burden_count");
        builder.Property(f => f.UpdatedAt).HasColumnName("updated_at");
        // fairness_counters has no created_at column — ignore the base Entity property
        builder.Ignore(f => f.CreatedAt);
        builder.HasIndex(f => new { f.SpaceId, f.PersonId, f.AsOfDate }).IsUnique();
    }
}
