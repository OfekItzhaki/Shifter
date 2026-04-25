using Jobuler.Domain.People;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jobuler.Infrastructure.Persistence.Configurations;

public class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> builder)
    {
        builder.ToTable("people");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.SpaceId).HasColumnName("space_id");
        builder.Property(p => p.LinkedUserId).HasColumnName("linked_user_id");
        builder.Property(p => p.FullName).HasColumnName("full_name").IsRequired();
        builder.Property(p => p.DisplayName).HasColumnName("display_name");
        builder.Property(p => p.ProfileImageUrl).HasColumnName("profile_image_url");
        builder.Property(p => p.IsActive).HasColumnName("is_active");
        builder.Property(p => p.CreatedAt).HasColumnName("created_at");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");
        builder.Property(p => p.PhoneNumber).HasColumnName("phone_number");
        builder.Property(p => p.InvitationStatus).HasColumnName("invitation_status")
            .HasDefaultValue("accepted");
    }
}

public class PersonQualificationConfiguration : IEntityTypeConfiguration<PersonQualification>
{
    public void Configure(EntityTypeBuilder<PersonQualification> builder)
    {
        builder.ToTable("person_qualifications");
        builder.HasKey(q => q.Id);
        builder.Property(q => q.Id).HasColumnName("id");
        builder.Property(q => q.SpaceId).HasColumnName("space_id");
        builder.Property(q => q.PersonId).HasColumnName("person_id");
        builder.Property(q => q.Qualification).HasColumnName("qualification").IsRequired();
        builder.Property(q => q.IssuedAt).HasColumnName("issued_at");
        builder.Property(q => q.ExpiresAt).HasColumnName("expires_at");
        builder.Property(q => q.IsActive).HasColumnName("is_active");
        builder.Property(q => q.CreatedAt).HasColumnName("created_at");
    }
}

public class AvailabilityWindowConfiguration : IEntityTypeConfiguration<AvailabilityWindow>
{
    public void Configure(EntityTypeBuilder<AvailabilityWindow> builder)
    {
        builder.ToTable("availability_windows");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.SpaceId).HasColumnName("space_id");
        builder.Property(a => a.PersonId).HasColumnName("person_id");
        builder.Property(a => a.StartsAt).HasColumnName("starts_at");
        builder.Property(a => a.EndsAt).HasColumnName("ends_at");
        builder.Property(a => a.Note).HasColumnName("note");
        builder.Property(a => a.CreatedAt).HasColumnName("created_at");
    }
}

public class PresenceWindowConfiguration : IEntityTypeConfiguration<PresenceWindow>
{
    public void Configure(EntityTypeBuilder<PresenceWindow> builder)
    {
        builder.ToTable("presence_windows");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.SpaceId).HasColumnName("space_id");
        builder.Property(p => p.PersonId).HasColumnName("person_id");
        builder.Property(p => p.State).HasColumnName("state")
            .HasConversion(
                v => v.ToString().ToSnakeCase(),
                v => Enum.Parse<Jobuler.Domain.People.PresenceState>(v.ToPascalCase()));
        builder.Property(p => p.StartsAt).HasColumnName("starts_at");
        builder.Property(p => p.EndsAt).HasColumnName("ends_at");
        builder.Property(p => p.Note).HasColumnName("note");
        builder.Property(p => p.IsDerived).HasColumnName("is_derived");
        builder.Property(p => p.CreatedAt).HasColumnName("created_at");
    }
}

public class PersonRestrictionConfiguration : IEntityTypeConfiguration<PersonRestriction>
{
    public void Configure(EntityTypeBuilder<PersonRestriction> builder)
    {
        builder.ToTable("person_restrictions");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.SpaceId).HasColumnName("space_id");
        builder.Property(r => r.PersonId).HasColumnName("person_id");
        builder.Property(r => r.RestrictionType).HasColumnName("restriction_type").IsRequired();
        builder.Property(r => r.TaskTypeId).HasColumnName("task_type_id");
        builder.Property(r => r.EffectiveFrom).HasColumnName("effective_from");
        builder.Property(r => r.EffectiveUntil).HasColumnName("effective_until");
        builder.Property(r => r.OperationalNote).HasColumnName("operational_note");
        builder.Property(r => r.CreatedByUserId).HasColumnName("created_by_user_id");
        builder.Property(r => r.CreatedAt).HasColumnName("created_at");
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");
    }
}

public class SensitiveRestrictionReasonConfiguration : IEntityTypeConfiguration<SensitiveRestrictionReason>
{
    public void Configure(EntityTypeBuilder<SensitiveRestrictionReason> builder)
    {
        builder.ToTable("sensitive_restriction_reasons");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.SpaceId).HasColumnName("space_id");
        builder.Property(s => s.RestrictionId).HasColumnName("restriction_id");
        builder.Property(s => s.Reason).HasColumnName("reason").IsRequired();
        builder.Property(s => s.CreatedByUserId).HasColumnName("created_by_user_id");
        builder.Property(s => s.CreatedAt).HasColumnName("created_at");
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");
    }
}
