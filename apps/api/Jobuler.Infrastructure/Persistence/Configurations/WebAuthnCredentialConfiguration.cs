using Jobuler.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jobuler.Infrastructure.Persistence.Configurations;

public class WebAuthnCredentialConfiguration : IEntityTypeConfiguration<WebAuthnCredential>
{
    public void Configure(EntityTypeBuilder<WebAuthnCredential> builder)
    {
        builder.ToTable("webauthn_credentials");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.UserId).HasColumnName("user_id");
        builder.Property(e => e.CredentialId).HasColumnName("credential_id").HasColumnType("bytea").IsRequired();
        builder.Property(e => e.PublicKey).HasColumnName("public_key").HasColumnType("bytea").IsRequired();
        builder.Property(e => e.SignCount).HasColumnName("sign_count").IsRequired();
        builder.Property(e => e.Transports).HasColumnName("transports").HasColumnType("text[]");
        builder.Property(e => e.Nickname).HasColumnName("nickname").HasMaxLength(100);
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.LastUsedAt).HasColumnName("last_used_at");
        builder.Property(e => e.IsDisabled).HasColumnName("is_disabled").HasDefaultValue(false);

        builder.HasIndex(e => e.CredentialId).IsUnique();
        builder.HasIndex(e => e.UserId);

        builder.HasOne(e => e.User)
               .WithMany()
               .HasForeignKey(e => e.UserId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
