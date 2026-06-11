using Jobuler.Domain.Spaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jobuler.Infrastructure.Persistence.Configurations;

public class SpaceSelfServiceDefaultsConfiguration : IEntityTypeConfiguration<SpaceSelfServiceDefaults>
{
    public void Configure(EntityTypeBuilder<SpaceSelfServiceDefaults> builder)
    {
        builder.ToTable("space_self_service_defaults");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.SpaceId).HasColumnName("space_id");
        builder.Property(c => c.MinShiftsPerCycle).HasColumnName("min_shifts_per_cycle").HasDefaultValue(0);
        builder.Property(c => c.MaxShiftsPerCycle).HasColumnName("max_shifts_per_cycle").HasDefaultValue(7);
        builder.Property(c => c.RequestWindowOpenOffsetHours).HasColumnName("request_window_open_offset_hours").HasDefaultValue(168);
        builder.Property(c => c.RequestWindowCloseOffsetHours).HasColumnName("request_window_close_offset_hours").HasDefaultValue(24);
        builder.Property(c => c.CancellationCutoffHours).HasColumnName("cancellation_cutoff_hours").HasDefaultValue(24);
        builder.Property(c => c.MaxAbsencesPerCycle).HasColumnName("max_absences_per_cycle").HasDefaultValue(3);
        builder.Property(c => c.MaxLateCancellationsPerCycle).HasColumnName("max_late_cancellations_per_cycle").HasDefaultValue(2);
        builder.Property(c => c.LateCancellationWindowHours).HasColumnName("late_cancellation_window_hours").HasDefaultValue(24);
        builder.Property(c => c.WaitlistOfferMinutes).HasColumnName("waitlist_offer_minutes").HasDefaultValue(60);
        builder.Property(c => c.CycleDurationDays).HasColumnName("cycle_duration_days").HasDefaultValue(7);
        builder.Property(c => c.AllowMemberShiftClaims).HasColumnName("allow_member_shift_claims").HasDefaultValue(true);
        builder.Property(c => c.AllowWaitlist).HasColumnName("allow_waitlist").HasDefaultValue(true);
        builder.Property(c => c.AllowShiftChangeRequests).HasColumnName("allow_shift_change_requests").HasDefaultValue(true);
        builder.Property(c => c.AllowAbsenceReports).HasColumnName("allow_absence_reports").HasDefaultValue(true);
        builder.Property(c => c.AllowShiftSwaps).HasColumnName("allow_shift_swaps").HasDefaultValue(true);
        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(c => c.SpaceId).IsUnique();
    }
}
