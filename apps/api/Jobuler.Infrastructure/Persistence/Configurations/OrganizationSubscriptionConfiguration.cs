using Jobuler.Domain.Billing;
using Jobuler.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jobuler.Infrastructure.Persistence.Configurations;

public class OrganizationSubscriptionConfiguration : IEntityTypeConfiguration<OrganizationSubscription>
{
    public void Configure(EntityTypeBuilder<OrganizationSubscription> builder)
    {
        builder.ToTable("organization_subscriptions");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.OrganizationId).HasColumnName("organization_id").IsRequired();
        builder.Property(s => s.BillingMode).HasColumnName("billing_mode").HasConversion<string>().IsRequired();
        builder.Property(s => s.TierId).HasColumnName("tier_id").IsRequired();
        builder.Property(s => s.Status).HasColumnName("status").HasConversion<string>().IsRequired();
        builder.Property(s => s.ProviderSubscriptionId).HasColumnName("provider_subscription_id");
        builder.Property(s => s.ProviderCustomerId).HasColumnName("provider_customer_id");
        builder.Property(s => s.CurrentPeriodStart).HasColumnName("current_period_start").IsRequired();
        builder.Property(s => s.CurrentPeriodEnd).HasColumnName("current_period_end");
        builder.Property(s => s.CoveredSpaceLimit).HasColumnName("covered_space_limit");
        builder.Property(s => s.CoveredMemberLimit).HasColumnName("covered_member_limit");
        builder.Property(s => s.AutoRenew).HasColumnName("auto_renew").IsRequired();
        builder.Property(s => s.CanceledAt).HasColumnName("canceled_at");
        builder.Property(s => s.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(s => s.OrganizationId).IsUnique()
            .HasDatabaseName("uq_organization_subscriptions_organization_id");
        builder.HasIndex(s => s.Status)
            .HasDatabaseName("idx_organization_subscriptions_status");

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(s => s.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
