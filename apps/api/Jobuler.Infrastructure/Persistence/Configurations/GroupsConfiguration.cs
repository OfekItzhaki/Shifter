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
        builder.Property(m => m.JoinedAt).HasColumnName("joined_at");
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
        builder.Property(r => r.AssignedAt).HasColumnName("assigned_at");
        builder.HasIndex(r => new { r.PersonId, r.RoleId }).IsUnique();
    }
}
