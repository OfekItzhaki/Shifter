using Jobuler.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jobuler.Infrastructure.Persistence.Configurations;

public class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("organizations");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).HasColumnName("id");
        builder.Property(o => o.DisplayName).HasColumnName("display_name").IsRequired().HasMaxLength(200);
        builder.Property(o => o.NormalizedName).HasColumnName("normalized_name").IsRequired().HasMaxLength(200);
        builder.Property(o => o.PrimaryOwnerUserId).HasColumnName("primary_owner_user_id");
        builder.Property(o => o.CountryCode).HasColumnName("country_code").HasMaxLength(2);
        builder.Property(o => o.SetupTemplate).HasColumnName("setup_template").HasMaxLength(80);
        builder.Property(o => o.DefaultLocale).HasColumnName("default_locale").HasMaxLength(12);
        builder.Property(o => o.DefaultTimezoneId).HasColumnName("default_timezone_id").HasMaxLength(100);
        builder.Property(o => o.Status).HasColumnName("status").HasConversion<string>().IsRequired();
        builder.Property(o => o.RelocatedAt).HasColumnName("relocated_at");
        builder.Property(o => o.DisabledAt).HasColumnName("disabled_at");
        builder.Property(o => o.PurgeEligibleAt).HasColumnName("purge_eligible_at");
        builder.Property(o => o.DedicatedDeploymentKey).HasColumnName("dedicated_deployment_key").HasMaxLength(120);
        builder.Property(o => o.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(o => o.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(o => o.PrimaryOwnerUserId)
            .HasDatabaseName("idx_organizations_primary_owner_user_id");
        builder.HasIndex(o => o.NormalizedName)
            .HasDatabaseName("idx_organizations_normalized_name");
        builder.HasIndex(o => new { o.CountryCode, o.SetupTemplate })
            .HasDatabaseName("idx_organizations_country_template");
        builder.HasIndex(o => o.Status)
            .HasDatabaseName("idx_organizations_status");
        builder.HasIndex(o => o.PurgeEligibleAt)
            .HasDatabaseName("idx_organizations_purge_eligible_at");
    }
}
