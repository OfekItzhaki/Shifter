using Jobuler.Domain.Identity;
using Jobuler.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jobuler.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasColumnName("id");
        builder.Property(u => u.Email)
            .HasColumnName("email")
            .HasConversion(value => FieldEncryption.Encrypt(value), value => FieldEncryption.Decrypt(value))
            .IsRequired();
        builder.Property(u => u.DisplayName).HasColumnName("display_name").IsRequired();
        builder.Property(u => u.PasswordHash).HasColumnName("password_hash").IsRequired();
        builder.Property(u => u.IsActive).HasColumnName("is_active");
        builder.Property(u => u.PreferredLocale).HasColumnName("preferred_locale");
        builder.Property(u => u.ProfileImageUrl).HasColumnName("profile_image_url");
        builder.Property(u => u.LastLoginAt).HasColumnName("last_login_at");
        builder.Property(u => u.CreatedAt).HasColumnName("created_at");
        builder.Property(u => u.UpdatedAt).HasColumnName("updated_at");
        builder.Property(u => u.PhoneNumber)
            .HasColumnName("phone_number")
            .HasConversion(value => FieldEncryption.EncryptNullable(value), value => FieldEncryption.DecryptNullable(value));
        builder.Property(u => u.EmailLookupHash).HasColumnName("email_lookup_hash").HasMaxLength(64);
        builder.Property(u => u.PhoneLookupHash).HasColumnName("phone_lookup_hash").HasMaxLength(64);
        builder.Property(u => u.Birthday).HasColumnName("birthday");
        builder.Property(u => u.IsPlatformAdmin).HasColumnName("is_platform_admin");
        builder.Property(u => u.EmailVerified).HasColumnName("email_verified").HasDefaultValue(false);
        builder.Property(u => u.CountryCode).HasColumnName("country_code").HasMaxLength(2).IsRequired(false);
        builder.Property(u => u.StateCode).HasColumnName("state_code").HasMaxLength(6).IsRequired(false);
        builder.HasIndex(u => u.EmailLookupHash).IsUnique();
        builder.HasIndex(u => u.PhoneLookupHash).IsUnique().HasFilter("phone_lookup_hash IS NOT NULL");
    }
}
