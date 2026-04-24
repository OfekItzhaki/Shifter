using Jobuler.Domain.Spaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jobuler.Infrastructure.Persistence.Configurations;

public class SpaceConfiguration : IEntityTypeConfiguration<Space>
{
    public void Configure(EntityTypeBuilder<Space> builder)
    {
        builder.ToTable("spaces");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.Name).HasColumnName("name").IsRequired();
        builder.Property(s => s.Description).HasColumnName("description");
        builder.Property(s => s.OwnerUserId).HasColumnName("owner_user_id");
        builder.Property(s => s.IsActive).HasColumnName("is_active");
        builder.Property(s => s.Locale).HasColumnName("locale");
        builder.Property(s => s.CreatedAt).HasColumnName("created_at");
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");
    }
}

public class SpaceMembershipConfiguration : IEntityTypeConfiguration<SpaceMembership>
{
    public void Configure(EntityTypeBuilder<SpaceMembership> builder)
    {
        builder.ToTable("space_memberships");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id");
        builder.Property(m => m.SpaceId).HasColumnName("space_id");
        builder.Property(m => m.UserId).HasColumnName("user_id");
        builder.Property(m => m.JoinedAt).HasColumnName("joined_at");
        builder.Property(m => m.IsActive).HasColumnName("is_active");
        builder.Ignore(m => m.CreatedAt); // table uses joined_at, no created_at column
        builder.HasIndex(m => new { m.SpaceId, m.UserId }).IsUnique();
    }
}

public class SpacePermissionGrantConfiguration : IEntityTypeConfiguration<SpacePermissionGrant>
{
    public void Configure(EntityTypeBuilder<SpacePermissionGrant> builder)
    {
        builder.ToTable("space_permission_grants");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).HasColumnName("id");
        builder.Property(g => g.SpaceId).HasColumnName("space_id");
        builder.Property(g => g.UserId).HasColumnName("user_id");
        builder.Property(g => g.PermissionKey).HasColumnName("permission_key").IsRequired();
        builder.Property(g => g.GrantedByUserId).HasColumnName("granted_by_user_id");
        builder.Property(g => g.GrantedAt).HasColumnName("granted_at");
        builder.Property(g => g.RevokedAt).HasColumnName("revoked_at");
        builder.Ignore(g => g.CreatedAt); // table uses granted_at, no created_at column
        builder.HasIndex(g => new { g.SpaceId, g.UserId, g.PermissionKey }).IsUnique();
    }
}

public class SpaceRoleConfiguration : IEntityTypeConfiguration<SpaceRole>
{
    public void Configure(EntityTypeBuilder<SpaceRole> builder)
    {
        builder.ToTable("space_roles");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.SpaceId).HasColumnName("space_id");
        builder.Property(r => r.Name).HasColumnName("name").IsRequired();
        builder.Property(r => r.Description).HasColumnName("description");
        builder.Property(r => r.IsActive).HasColumnName("is_active");
        builder.Property(r => r.CreatedByUserId).HasColumnName("created_by_user_id");
        builder.Property(r => r.CreatedAt).HasColumnName("created_at");
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");
        builder.HasIndex(r => new { r.SpaceId, r.Name }).IsUnique();
    }
}

public class OwnershipTransferHistoryConfiguration : IEntityTypeConfiguration<OwnershipTransferHistory>
{
    public void Configure(EntityTypeBuilder<OwnershipTransferHistory> builder)
    {
        builder.ToTable("ownership_transfer_history");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).HasColumnName("id");
        builder.Property(o => o.SpaceId).HasColumnName("space_id");
        builder.Property(o => o.PreviousOwnerId).HasColumnName("previous_owner_id");
        builder.Property(o => o.NewOwnerId).HasColumnName("new_owner_id");
        builder.Property(o => o.TransferredByUserId).HasColumnName("transferred_by_user_id");
        builder.Property(o => o.Reason).HasColumnName("reason");
        builder.Property(o => o.TransferredAt).HasColumnName("transferred_at");
        builder.Ignore(o => o.CreatedAt); // table uses transferred_at, no created_at column
    }
}
