using Jobuler.Domain.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jobuler.Infrastructure.Persistence.Configurations;

public class DailySnapshotConfiguration : IEntityTypeConfiguration<DailySnapshot>
{
    public void Configure(EntityTypeBuilder<DailySnapshot> builder)
    {
        builder.ToTable("daily_snapshots");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.SpaceId).HasColumnName("space_id");
        builder.Property(s => s.GroupId).HasColumnName("group_id");
        builder.Property(s => s.PersonId).HasColumnName("person_id");
        builder.Property(s => s.PeriodId).HasColumnName("period_id");
        builder.Property(s => s.SnapshotDate).HasColumnName("snapshot_date")
            .HasColumnType("date");
        builder.Property(s => s.TaskTypeId).HasColumnName("task_type_id");
        builder.Property(s => s.SlotId).HasColumnName("slot_id");
        builder.Property(s => s.ShiftStart).HasColumnName("shift_start");
        builder.Property(s => s.ShiftEnd).HasColumnName("shift_end");
        builder.Property(s => s.BurdenLevel).HasColumnName("burden_level");
        builder.Property(s => s.VersionId).HasColumnName("version_id");
        builder.Property(s => s.CreatedAt).HasColumnName("created_at");
        builder.Ignore(s => s.IsPast);

        // Unique constraint
        builder.HasIndex(s => new { s.SpaceId, s.GroupId, s.PersonId, s.SnapshotDate, s.SlotId })
            .IsUnique();

        // Indexes
        builder.HasIndex(s => new { s.SpaceId, s.GroupId, s.SnapshotDate })
            .HasDatabaseName("idx_daily_snapshots_date_range");
        builder.HasIndex(s => new { s.PersonId, s.SnapshotDate })
            .HasDatabaseName("idx_daily_snapshots_person");
        builder.HasIndex(s => new { s.PeriodId, s.SnapshotDate })
            .HasDatabaseName("idx_daily_snapshots_period");
    }
}
