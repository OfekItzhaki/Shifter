using Jobuler.Domain.People;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jobuler.Infrastructure.Persistence.Configurations;

public class PendingInvitationConfiguration : IEntityTypeConfiguration<PendingInvitation>
{
    public void Configure(EntityTypeBuilder<PendingInvitation> builder)
    {
        builder.ToTable("pending_invitations");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasColumnName("id");
        builder.Property(i => i.SpaceId).HasColumnName("space_id");
        builder.Property(i => i.PersonId).HasColumnName("person_id");
        builder.Property(i => i.Contact).HasColumnName("contact").IsRequired();
        builder.Property(i => i.Channel).HasColumnName("channel").IsRequired();
        builder.Property(i => i.TokenHash).HasColumnName("token_hash").IsRequired();
        builder.Property(i => i.IsAccepted).HasColumnName("is_accepted");
        builder.Property(i => i.ExpiresAt).HasColumnName("expires_at");
        builder.Property(i => i.InvitedByUserId).HasColumnName("invited_by_user_id");
        builder.Property(i => i.CreatedAt).HasColumnName("created_at");
        builder.HasIndex(i => i.TokenHash).IsUnique();
        builder.HasIndex(i => new { i.SpaceId, i.PersonId });
    }
}
