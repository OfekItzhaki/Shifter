using Jobuler.Domain.Constraints;
using Jobuler.Domain.Groups;
using Jobuler.Domain.Identity;
using Jobuler.Domain.Logs;
using Jobuler.Domain.Notifications;
using Jobuler.Domain.People;
using Jobuler.Domain.Scheduling;
using Jobuler.Domain.Spaces;
using Jobuler.Domain.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Infrastructure.Persistence;

/// <summary>
/// Moved to Application so handlers can reference it without a circular dependency.
/// EF configurations are still applied from Infrastructure's assembly via the
/// Infrastructure project's DI registration.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // Identity
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    // Spaces
    public DbSet<Space> Spaces => Set<Space>();
    public DbSet<SpaceMembership> SpaceMemberships => Set<SpaceMembership>();
    public DbSet<SpacePermissionGrant> SpacePermissionGrants => Set<SpacePermissionGrant>();
    public DbSet<SpaceRole> SpaceRoles => Set<SpaceRole>();
    public DbSet<OwnershipTransferHistory> OwnershipTransferHistory => Set<OwnershipTransferHistory>();

    // People
    public DbSet<Person> People => Set<Person>();
    public DbSet<PersonQualification> PersonQualifications => Set<PersonQualification>();
    public DbSet<AvailabilityWindow> AvailabilityWindows => Set<AvailabilityWindow>();
    public DbSet<PresenceWindow> PresenceWindows => Set<PresenceWindow>();
    public DbSet<PersonRestriction> PersonRestrictions => Set<PersonRestriction>();
    public DbSet<SensitiveRestrictionReason> SensitiveRestrictionReasons => Set<SensitiveRestrictionReason>();

    // Groups
    public DbSet<GroupType> GroupTypes => Set<GroupType>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<GroupMembership> GroupMemberships => Set<GroupMembership>();
    public DbSet<PersonRoleAssignment> PersonRoleAssignments => Set<PersonRoleAssignment>();

    // Tasks
    public DbSet<TaskType> TaskTypes => Set<TaskType>();
    public DbSet<TaskSlot> TaskSlots => Set<TaskSlot>();
    public DbSet<TaskTypeOverlapRule> TaskTypeOverlapRules => Set<TaskTypeOverlapRule>();

    // Constraints
    public DbSet<ConstraintRule> ConstraintRules => Set<ConstraintRule>();

    // Scheduling
    public DbSet<ScheduleRun> ScheduleRuns => Set<ScheduleRun>();
    public DbSet<ScheduleVersion> ScheduleVersions => Set<ScheduleVersion>();
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<AssignmentChangeSummary> AssignmentChangeSummaries => Set<AssignmentChangeSummary>();
    public DbSet<FairnessCounter> FairnessCounters => Set<FairnessCounter>();

    // Logs
    public DbSet<SystemLog> SystemLogs => Set<SystemLog>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // Notifications
    public DbSet<Notification> Notifications => Set<Notification>();

    /// <summary>
    /// Set by Infrastructure at startup so OnModelCreating can apply EF configurations
    /// from the Infrastructure assembly without a circular project reference.
    /// </summary>
    public static System.Reflection.Assembly? ConfigurationAssembly { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        if (ConfigurationAssembly is not null)
            modelBuilder.ApplyConfigurationsFromAssembly(ConfigurationAssembly);
    }
}
