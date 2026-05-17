using Jobuler.Domain.Groups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jobuler.Infrastructure.Persistence.Configurations;

public class GroupTypeConfiguration : IEntityTypeConfiguration<GroupType>
{
    public void Configure(EntityTypeBuilder<GroupType> builder)
    {
        builder.ToTable("group_types");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).HasColumnName("id");
        builder.Property(g => g.SpaceId).HasColumnName("space_id");
        builder.Property(g => g.Name).HasColumnName("name").IsRequired();
        builder.Property(g => g.Description).HasColumnName("description");
        builder.Property(g => g.IsActive).HasColumnName("is_active");
        builder.Property(g => g.CreatedAt).HasColumnName("created_at");
        builder.HasIndex(g => new { g.SpaceId, g.Name }).IsUnique();
    }
}

public class GroupConfiguration : IEntityTypeConfiguration<Group>
{
    public void Configure(EntityTypeBuilder<Group> builder)
    {
        builder.ToTable("groups");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).HasColumnName("id");
        builder.Property(g => g.SpaceId).HasColumnName("space_id");
        builder.Property(g => g.GroupTypeId).HasColumnName("group_type_id").IsRequired(false);
        builder.Property(g => g.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired(false);
        builder.Property(g => g.Name).HasColumnName("name").IsRequired();
        builder.Property(g => g.Description).HasColumnName("description");
        builder.Property(g => g.IsActive).HasColumnName("is_active");
        builder.Property(g => g.SolverHorizonDays).HasColumnName("solver_horizon_days").HasDefaultValue(7);
        builder.Property(g => g.SolverStartDateTime).HasColumnName("solver_start_date_time").IsRequired(false);
        builder.Property(g => g.AutoPublish).HasColumnName("auto_publish").HasDefaultValue(false);
        builder.Property(g => g.IsClosedBase).HasColumnName("is_closed_base").HasDefaultValue(false);
        builder.Property(g => g.MinRestBetweenShiftsHours).HasColumnName("min_rest_between_shifts_hours").HasDefaultValue(8);
        builder.Property(g => g.JoinCode).HasColumnName("join_code").HasMaxLength(8).IsRequired(false);
        builder.HasIndex(g => g.JoinCode).IsUnique().HasFilter("join_code IS NOT NULL");
        builder.Property(g => g.TemplateType).HasColumnName("template_type")
            .HasConversion(v => v.ToString(), v => Enum.Parse<GroupTemplateType>(v, true))
            .HasDefaultValue(GroupTemplateType.Custom);
        builder.Property(g => g.AllowMembersViewHistory).HasColumnName("allow_members_view_history").HasDefaultValue(true);
        builder.Property(g => g.AllowMembersViewStats).HasColumnName("allow_members_view_stats").HasDefaultValue(false);
        builder.Property(g => g.DeletedAt).HasColumnName("deleted_at");
        builder.Property(g => g.CreatedAt).HasColumnName("created_at");
        builder.Property(g => g.UpdatedAt).HasColumnName("updated_at");
        builder.HasIndex(g => new { g.SpaceId, g.Name }).IsUnique();
    }
}

public class GroupMembershipConfiguration : IEntityTypeConfiguration<GroupMembership>
{
    public void Configure(EntityTypeBuilder<GroupMembership> builder)
    {
        builder.ToTable("group_memberships");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id");
        builder.Property(m => m.SpaceId).HasColumnName("space_id");
        builder.Property(m => m.GroupId).HasColumnName("group_id");
        builder.Property(m => m.PersonId).HasColumnName("person_id");
        builder.Property(m => m.IsOwner).HasColumnName("is_owner");
        builder.Property(m => m.JoinedAt).HasColumnName("joined_at");
        builder.Property(m => m.HomeLeavePriority).HasColumnName("home_leave_priority")
            .HasColumnType("numeric(3,1)");
        builder.Ignore(m => m.CreatedAt);  // inherited from Entity but not in this table
        builder.HasIndex(m => new { m.GroupId, m.PersonId }).IsUnique();
    }
}

public class PersonRoleAssignmentConfiguration : IEntityTypeConfiguration<PersonRoleAssignment>
{
    public void Configure(EntityTypeBuilder<PersonRoleAssignment> builder)
    {
        builder.ToTable("person_role_assignments");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.SpaceId).HasColumnName("space_id");
        builder.Property(r => r.PersonId).HasColumnName("person_id");
        builder.Property(r => r.RoleId).HasColumnName("role_id");
        builder.Property(r => r.GroupId).HasColumnName("group_id").IsRequired(false);
        builder.Property(r => r.AssignedAt).HasColumnName("assigned_at");
        builder.Ignore(r => r.CreatedAt); // table uses assigned_at, no created_at column
        // Unique index is now (person_id, role_id, group_id) — enforced at DB level via migration 027
    }
}

public class GroupInvitationConfiguration : IEntityTypeConfiguration<GroupInvitation>
{
    public void Configure(EntityTypeBuilder<GroupInvitation> builder)
    {
        builder.ToTable("group_invitations");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasColumnName("id");
        builder.Property(i => i.SpaceId).HasColumnName("space_id");
        builder.Property(i => i.GroupId).HasColumnName("group_id");
        builder.Property(i => i.Email).HasColumnName("email").IsRequired();
        builder.Property(i => i.PersonId).HasColumnName("person_id").IsRequired(false);
        builder.Property(i => i.InvitedByUserId).HasColumnName("invited_by_user_id").IsRequired(false);
        builder.Property(i => i.OptOutToken).HasColumnName("opt_out_token").IsRequired();
        builder.Property(i => i.Status).HasColumnName("status").HasDefaultValue("active");
        builder.Property(i => i.CreatedAt).HasColumnName("created_at");
        builder.Property(i => i.OptedOutAt).HasColumnName("opted_out_at").IsRequired(false);
        builder.HasIndex(i => i.OptOutToken).IsUnique();
    }
}
