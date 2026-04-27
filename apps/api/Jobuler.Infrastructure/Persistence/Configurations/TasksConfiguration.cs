using Jobuler.Domain.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jobuler.Infrastructure.Persistence.Configurations;

public class TaskTypeConfiguration : IEntityTypeConfiguration<TaskType>
{
    public void Configure(EntityTypeBuilder<TaskType> builder)
    {
        builder.ToTable("task_types");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.SpaceId).HasColumnName("space_id");
        builder.Property(t => t.Name).HasColumnName("name").IsRequired();
        builder.Property(t => t.Description).HasColumnName("description");
        builder.Property(t => t.BurdenLevel).HasColumnName("burden_level")
            .HasConversion(v => v.ToString().ToLower(), v => Enum.Parse<TaskBurdenLevel>(v, true));
        builder.Property(t => t.DefaultPriority).HasColumnName("default_priority");
        builder.Property(t => t.AllowsOverlap).HasColumnName("allows_overlap");
        builder.Property(t => t.IsActive).HasColumnName("is_active");
        builder.Property(t => t.CreatedByUserId).HasColumnName("created_by_user_id");
        builder.Property(t => t.CreatedAt).HasColumnName("created_at");
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at");
        builder.HasIndex(t => new { t.SpaceId, t.Name }).IsUnique();
    }
}

public class TaskSlotConfiguration : IEntityTypeConfiguration<TaskSlot>
{
    public void Configure(EntityTypeBuilder<TaskSlot> builder)
    {
        builder.ToTable("task_slots");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.SpaceId).HasColumnName("space_id");
        builder.Property(s => s.TaskTypeId).HasColumnName("task_type_id");
        builder.Property(s => s.StartsAt).HasColumnName("starts_at");
        builder.Property(s => s.EndsAt).HasColumnName("ends_at");
        builder.Property(s => s.RequiredHeadcount).HasColumnName("required_headcount");
        builder.Property(s => s.Priority).HasColumnName("priority");
        builder.Property(s => s.RequiredRoleIds).HasColumnName("required_role_ids_json")
            .HasConversion(
                v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                v => System.Text.Json.JsonSerializer.Deserialize<List<Guid>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new());
        builder.Property(s => s.RequiredQualificationIds).HasColumnName("required_qualification_ids_json")
            .HasConversion(
                v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                v => System.Text.Json.JsonSerializer.Deserialize<List<Guid>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new());
        builder.Property(s => s.Status).HasColumnName("status")
            .HasConversion(v => v.ToString().ToLower(), v => Enum.Parse<TaskSlotStatus>(v, true));
        builder.Property(s => s.Location).HasColumnName("location");
        builder.Property(s => s.CreatedByUserId).HasColumnName("created_by_user_id");
        builder.Property(s => s.CreatedAt).HasColumnName("created_at");
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");
    }
}

public class TaskTypeOverlapRuleConfiguration : IEntityTypeConfiguration<TaskTypeOverlapRule>
{
    public void Configure(EntityTypeBuilder<TaskTypeOverlapRule> builder)
    {
        builder.ToTable("task_type_overlap_rules");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.SpaceId).HasColumnName("space_id");
        builder.Property(r => r.TaskTypeAId).HasColumnName("task_type_a_id");
        builder.Property(r => r.TaskTypeBId).HasColumnName("task_type_b_id");
        builder.Property(r => r.OverlapAllowed).HasColumnName("overlap_allowed");
        builder.Property(r => r.CreatedAt).HasColumnName("created_at");
        builder.HasIndex(r => new { r.TaskTypeAId, r.TaskTypeBId }).IsUnique();
    }
}

public class GroupTaskConfiguration : IEntityTypeConfiguration<GroupTask>
{
    public void Configure(EntityTypeBuilder<GroupTask> builder)
    {
        builder.ToTable("tasks");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.SpaceId).HasColumnName("space_id");
        builder.Property(t => t.GroupId).HasColumnName("group_id");
        builder.Property(t => t.Name).HasColumnName("name").IsRequired();
        builder.Property(t => t.StartsAt).HasColumnName("starts_at");
        builder.Property(t => t.EndsAt).HasColumnName("ends_at");
        builder.Property(t => t.ShiftDurationMinutes).HasColumnName("shift_duration_minutes");
        builder.Property(t => t.RequiredHeadcount).HasColumnName("required_headcount");
        builder.Property(t => t.BurdenLevel).HasColumnName("burden_level")
            .HasConversion(v => v.ToString().ToLower(), v => Enum.Parse<TaskBurdenLevel>(v, true));
        builder.Property(t => t.AllowsDoubleShift).HasColumnName("allows_double_shift");
        builder.Property(t => t.AllowsOverlap).HasColumnName("allows_overlap");
        builder.Property(t => t.IsActive).HasColumnName("is_active");
        builder.Property(t => t.CreatedByUserId).HasColumnName("created_by_user_id");
        builder.Property(t => t.UpdatedByUserId).HasColumnName("updated_by_user_id");
        builder.Property(t => t.CreatedAt).HasColumnName("created_at");
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at");
        builder.HasIndex(t => new { t.SpaceId, t.GroupId, t.Name }).IsUnique();
    }
}
