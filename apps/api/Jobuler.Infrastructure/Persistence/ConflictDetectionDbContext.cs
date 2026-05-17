using Jobuler.Domain.Groups;
using Jobuler.Domain.Notifications;
using Jobuler.Domain.People;
using Jobuler.Domain.Scheduling;
using Jobuler.Domain.Spaces;
using Jobuler.Domain.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Infrastructure.Persistence;

/// <summary>
/// A minimal DbContext for cross-group conflict detection.
/// Registered WITHOUT the RLS session variable interceptor so it can query
/// across all spaces to resolve LinkedUserId-based person records.
/// 
/// Read-only for cross-space queries (assignments, task_slots, people, groups, etc.).
/// Write-only for notifications (always scoped to the affected user's space).
/// </summary>
public class ConflictDetectionDbContext : DbContext
{
    public ConflictDetectionDbContext(DbContextOptions<ConflictDetectionDbContext> options)
        : base(options) { }

    // Scheduling (read-only for conflict queries)
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<ScheduleVersion> ScheduleVersions => Set<ScheduleVersion>();

    // Tasks (read-only — for time ranges and group linkage)
    public DbSet<TaskSlot> TaskSlots => Set<TaskSlot>();
    public DbSet<GroupTask> GroupTasks => Set<GroupTask>();

    // People (read-only — for LinkedUserId resolution)
    public DbSet<Person> People => Set<Person>();

    // Groups (read-only — for MinRestBetweenShiftsHours and Name)
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<GroupMembership> GroupMemberships => Set<GroupMembership>();

    // Spaces (read-only — for locale)
    public DbSet<Space> Spaces => Set<Space>();

    // Notifications (write for creating conflict notifications, read for dedup check)
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<PushSubscription> PushSubscriptions => Set<PushSubscription>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // QualificationRequirement is a value object serialized as JSONB inside GroupTask.
        // EF must not treat it as a standalone entity.
        modelBuilder.Ignore<QualificationRequirement>();

        // Apply only the configurations relevant to this context from the Infrastructure assembly.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ConflictDetectionDbContext).Assembly);
    }
}
