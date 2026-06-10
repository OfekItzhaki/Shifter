using Jobuler.Domain.Groups;
using Jobuler.Domain.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jobuler.Infrastructure.Persistence.Configurations;

public class SelfServiceConfigConfiguration : IEntityTypeConfiguration<SelfServiceConfig>
{
    public void Configure(EntityTypeBuilder<SelfServiceConfig> builder)
    {
        builder.ToTable("self_service_configs");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.SpaceId).HasColumnName("space_id");
        builder.Property(c => c.GroupId).HasColumnName("group_id");
        builder.Property(c => c.MinShiftsPerCycle).HasColumnName("min_shifts_per_cycle").HasDefaultValue(0);
        builder.Property(c => c.MaxShiftsPerCycle).HasColumnName("max_shifts_per_cycle").HasDefaultValue(7);
        builder.Property(c => c.RequestWindowOpenOffsetHours).HasColumnName("request_window_open_offset_hours").HasDefaultValue(168);
        builder.Property(c => c.RequestWindowCloseOffsetHours).HasColumnName("request_window_close_offset_hours").HasDefaultValue(24);
        builder.Property(c => c.CancellationCutoffHours).HasColumnName("cancellation_cutoff_hours").HasDefaultValue(24);
        builder.Property(c => c.MaxLateCancellationsPerCycle).HasColumnName("max_late_cancellations_per_cycle").HasDefaultValue(2);
        builder.Property(c => c.LateCancellationWindowHours).HasColumnName("late_cancellation_window_hours").HasDefaultValue(24);
        builder.Property(c => c.WaitlistOfferMinutes).HasColumnName("waitlist_offer_minutes").HasDefaultValue(60);
        builder.Property(c => c.CycleDurationDays).HasColumnName("cycle_duration_days").HasDefaultValue(7);
        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(c => c.GroupId).IsUnique();
    }
}

public class SchedulingCycleConfiguration : IEntityTypeConfiguration<SchedulingCycle>
{
    public void Configure(EntityTypeBuilder<SchedulingCycle> builder)
    {
        builder.ToTable("scheduling_cycles");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.SpaceId).HasColumnName("space_id");
        builder.Property(c => c.GroupId).HasColumnName("group_id");
        builder.Property(c => c.StartsAt).HasColumnName("starts_at");
        builder.Property(c => c.EndsAt).HasColumnName("ends_at");
        builder.Property(c => c.RequestWindowOpensAt).HasColumnName("request_window_opens_at");
        builder.Property(c => c.RequestWindowClosesAt).HasColumnName("request_window_closes_at");
        builder.Property(c => c.IsGenerated).HasColumnName("is_generated").HasDefaultValue(false);
        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(c => new { c.GroupId, c.StartsAt, c.EndsAt })
            .HasDatabaseName("idx_scheduling_cycles_group_dates");
    }
}

public class ShiftTemplateConfiguration : IEntityTypeConfiguration<ShiftTemplate>
{
    public void Configure(EntityTypeBuilder<ShiftTemplate> builder)
    {
        builder.ToTable("shift_templates");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.SpaceId).HasColumnName("space_id");
        builder.Property(t => t.GroupId).HasColumnName("group_id");
        builder.Property(t => t.GroupTaskId).HasColumnName("group_task_id");
        builder.Property(t => t.DayOfWeek).HasColumnName("day_of_week");
        builder.Property(t => t.StartTime).HasColumnName("start_time");
        builder.Property(t => t.EndTime).HasColumnName("end_time");
        builder.Property(t => t.RequiredHeadcount).HasColumnName("required_headcount");
        builder.Property(t => t.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(t => t.CreatedByUserId).HasColumnName("created_by_user_id");
        builder.Property(t => t.CreatedAt).HasColumnName("created_at");
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(t => t.GroupId)
            .HasFilter("is_deleted = false")
            .HasDatabaseName("idx_shift_templates_group");
    }
}

public class ShiftSlotConfiguration : IEntityTypeConfiguration<ShiftSlot>
{
    public void Configure(EntityTypeBuilder<ShiftSlot> builder)
    {
        builder.ToTable("shift_slots");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.SpaceId).HasColumnName("space_id");
        builder.Property(s => s.GroupId).HasColumnName("group_id");
        builder.Property(s => s.GroupTaskId).HasColumnName("group_task_id");
        builder.Property(s => s.ShiftTemplateId).HasColumnName("shift_template_id");
        builder.Property(s => s.SchedulingCycleId).HasColumnName("scheduling_cycle_id");
        builder.Property(s => s.Date).HasColumnName("date");
        builder.Property(s => s.StartTime).HasColumnName("start_time");
        builder.Property(s => s.EndTime).HasColumnName("end_time");
        builder.Property(s => s.Capacity).HasColumnName("capacity");
        builder.Property(s => s.CurrentFillCount).HasColumnName("current_fill_count").HasDefaultValue(0);
        builder.Property(s => s.Status).HasColumnName("status")
            .HasConversion(v => v.ToString(), v => Enum.Parse<ShiftSlotStatus>(v, true))
            .HasDefaultValue(ShiftSlotStatus.Open);
        builder.Property(s => s.CreatedAt).HasColumnName("created_at");
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(s => new { s.ShiftTemplateId, s.Date, s.GroupId }).IsUnique();

        builder.HasIndex(s => new { s.SchedulingCycleId, s.Status })
            .HasDatabaseName("idx_shift_slots_cycle");

        builder.HasIndex(s => new { s.GroupId, s.Date, s.StartTime })
            .HasDatabaseName("idx_shift_slots_group_date");
    }
}

public class ShiftRequestConfiguration : IEntityTypeConfiguration<ShiftRequest>
{
    public void Configure(EntityTypeBuilder<ShiftRequest> builder)
    {
        builder.ToTable("shift_requests");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.SpaceId).HasColumnName("space_id");
        builder.Property(r => r.ShiftSlotId).HasColumnName("shift_slot_id");
        builder.Property(r => r.PersonId).HasColumnName("person_id");
        builder.Property(r => r.GroupId).HasColumnName("group_id");
        builder.Property(r => r.SchedulingCycleId).HasColumnName("scheduling_cycle_id");
        builder.Property(r => r.Status).HasColumnName("status")
            .HasConversion(v => v.ToString(), v => Enum.Parse<ShiftRequestStatus>(v, true))
            .HasDefaultValue(ShiftRequestStatus.Pending);
        builder.Property(r => r.IsAdminOverride).HasColumnName("is_admin_override").HasDefaultValue(false);
        builder.Property(r => r.ProcessedByUserId).HasColumnName("processed_by_user_id");
        builder.Property(r => r.RejectionReason).HasColumnName("rejection_reason");
        builder.Property(r => r.CancellationReason).HasColumnName("cancellation_reason");
        builder.Property(r => r.CancelledAt).HasColumnName("cancelled_at");
        builder.Property(r => r.CreatedAt).HasColumnName("created_at");
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(r => new { r.PersonId, r.SchedulingCycleId, r.Status })
            .HasDatabaseName("idx_shift_requests_person_cycle");

        builder.HasIndex(r => new { r.ShiftSlotId, r.Status })
            .HasDatabaseName("idx_shift_requests_slot");

        builder.HasIndex(r => new { r.ShiftSlotId, r.PersonId })
            .IsUnique()
            .HasFilter("status IN ('Pending', 'Approved')")
            .HasDatabaseName("idx_shift_requests_no_dup");
    }
}

public class ShiftAbsenceReportConfiguration : IEntityTypeConfiguration<ShiftAbsenceReport>
{
    public void Configure(EntityTypeBuilder<ShiftAbsenceReport> builder)
    {
        builder.ToTable("shift_absence_reports");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.SpaceId).HasColumnName("space_id");
        builder.Property(r => r.GroupId).HasColumnName("group_id");
        builder.Property(r => r.SchedulingCycleId).HasColumnName("scheduling_cycle_id");
        builder.Property(r => r.ShiftRequestId).HasColumnName("shift_request_id");
        builder.Property(r => r.ShiftSlotId).HasColumnName("shift_slot_id");
        builder.Property(r => r.PersonId).HasColumnName("person_id");
        builder.Property(r => r.Reason).HasColumnName("reason").HasMaxLength(500);
        builder.Property(r => r.IsLate).HasColumnName("is_late").HasDefaultValue(false);
        builder.Property(r => r.ReportedAt).HasColumnName("reported_at");
        builder.Property(r => r.Status).HasColumnName("status")
            .HasConversion(v => v.ToString(), v => Enum.Parse<ShiftAbsenceReportStatus>(v, true))
            .HasDefaultValue(ShiftAbsenceReportStatus.Pending);
        builder.Property(r => r.ReviewedByUserId).HasColumnName("reviewed_by_user_id");
        builder.Property(r => r.AdminNote).HasColumnName("admin_note").HasMaxLength(500);
        builder.Property(r => r.ReviewedAt).HasColumnName("reviewed_at");
        builder.Property(r => r.CreatedAt).HasColumnName("created_at");
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(r => new { r.PersonId, r.SchedulingCycleId, r.IsLate, r.Status })
            .HasDatabaseName("idx_shift_absence_reports_person_cycle");

        builder.HasIndex(r => new { r.GroupId, r.Status, r.ReportedAt })
            .HasDatabaseName("idx_shift_absence_reports_group_status");

        builder.HasIndex(r => r.ShiftRequestId)
            .IsUnique()
            .HasDatabaseName("idx_shift_absence_reports_shift_request");
    }
}

public class ShiftChangeRequestConfiguration : IEntityTypeConfiguration<ShiftChangeRequest>
{
    public void Configure(EntityTypeBuilder<ShiftChangeRequest> builder)
    {
        builder.ToTable("shift_change_requests");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.SpaceId).HasColumnName("space_id");
        builder.Property(r => r.GroupId).HasColumnName("group_id");
        builder.Property(r => r.SchedulingCycleId).HasColumnName("scheduling_cycle_id");
        builder.Property(r => r.ShiftRequestId).HasColumnName("shift_request_id");
        builder.Property(r => r.OriginalShiftSlotId).HasColumnName("original_shift_slot_id");
        builder.Property(r => r.RequestedShiftSlotId).HasColumnName("requested_shift_slot_id");
        builder.Property(r => r.PersonId).HasColumnName("person_id");
        builder.Property(r => r.Reason).HasColumnName("reason").HasMaxLength(500);
        builder.Property(r => r.Status).HasColumnName("status")
            .HasConversion(v => v.ToString(), v => Enum.Parse<ShiftChangeRequestStatus>(v, true))
            .HasDefaultValue(ShiftChangeRequestStatus.Pending);
        builder.Property(r => r.RequestedAt).HasColumnName("requested_at");
        builder.Property(r => r.ReviewedByUserId).HasColumnName("reviewed_by_user_id");
        builder.Property(r => r.AdminNote).HasColumnName("admin_note").HasMaxLength(500);
        builder.Property(r => r.ReviewedAt).HasColumnName("reviewed_at");
        builder.Property(r => r.CreatedAt).HasColumnName("created_at");
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(r => new { r.PersonId, r.SchedulingCycleId, r.Status })
            .HasDatabaseName("idx_shift_change_requests_person_cycle");

        builder.HasIndex(r => new { r.GroupId, r.Status, r.RequestedAt })
            .HasDatabaseName("idx_shift_change_requests_group_status");

        builder.HasIndex(r => r.ShiftRequestId)
            .HasFilter("status = 'Pending'")
            .HasDatabaseName("idx_shift_change_requests_shift_request_pending");

        builder.HasIndex(r => r.RequestedShiftSlotId)
            .HasDatabaseName("idx_shift_change_requests_requested_slot");
    }
}

public class WaitlistEntryConfiguration : IEntityTypeConfiguration<WaitlistEntry>
{
    public void Configure(EntityTypeBuilder<WaitlistEntry> builder)
    {
        builder.ToTable("waitlist_entries");
        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id).HasColumnName("id");
        builder.Property(w => w.SpaceId).HasColumnName("space_id");
        builder.Property(w => w.ShiftSlotId).HasColumnName("shift_slot_id");
        builder.Property(w => w.PersonId).HasColumnName("person_id");
        builder.Property(w => w.Position).HasColumnName("position");
        builder.Property(w => w.Status).HasColumnName("status")
            .HasConversion(v => v.ToString(), v => Enum.Parse<WaitlistEntryStatus>(v, true))
            .HasDefaultValue(WaitlistEntryStatus.Waiting);
        builder.Property(w => w.OfferedAt).HasColumnName("offered_at");
        builder.Property(w => w.ExpiresAt).HasColumnName("expires_at");
        builder.Property(w => w.CreatedAt).HasColumnName("created_at");
        builder.Property(w => w.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(w => new { w.ShiftSlotId, w.PersonId })
            .IsUnique()
            .HasFilter("status IN ('Waiting', 'Offered')")
            .HasDatabaseName("idx_waitlist_no_dup");

        builder.HasIndex(w => new { w.ShiftSlotId, w.Position })
            .HasFilter("status = 'Waiting'")
            .HasDatabaseName("idx_waitlist_slot_position");
    }
}

public class SwapRequestConfiguration : IEntityTypeConfiguration<SwapRequest>
{
    public void Configure(EntityTypeBuilder<SwapRequest> builder)
    {
        builder.ToTable("swap_requests");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.SpaceId).HasColumnName("space_id");
        builder.Property(s => s.GroupId).HasColumnName("group_id");
        builder.Property(s => s.InitiatorPersonId).HasColumnName("initiator_person_id");
        builder.Property(s => s.TargetPersonId).HasColumnName("target_person_id");
        builder.Property(s => s.InitiatorShiftRequestId).HasColumnName("initiator_shift_request_id");
        builder.Property(s => s.TargetShiftRequestId).HasColumnName("target_shift_request_id");
        builder.Property(s => s.Status).HasColumnName("status")
            .HasConversion(v => v.ToString(), v => Enum.Parse<SwapRequestStatus>(v, true))
            .HasDefaultValue(SwapRequestStatus.Pending);
        builder.Property(s => s.ExpiresAt).HasColumnName("expires_at");
        builder.Property(s => s.CreatedAt).HasColumnName("created_at");
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(s => new { s.Status, s.ExpiresAt })
            .HasFilter("status = 'Pending'")
            .HasDatabaseName("idx_swap_requests_status");
    }
}
