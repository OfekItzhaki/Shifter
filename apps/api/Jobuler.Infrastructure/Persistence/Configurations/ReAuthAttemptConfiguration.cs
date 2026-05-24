using Jobuler.Domain.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jobuler.Infrastructure.Persistence.Configurations;

public class ReAuthAttemptConfiguration : IEntityTypeConfiguration<ReAuthAttempt>
{
    public void Configure(EntityTypeBuilder<ReAuthAttempt> builder)
    {
        builder.ToTable("reauth_attempts");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(e => e.AttemptedAt).HasColumnName("attempted_at").IsRequired();
        builder.Property(e => e.Success).HasColumnName("success").IsRequired();
        builder.Property(e => e.Method).HasColumnName("method").HasColumnType("varchar(20)").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");

        // Composite index for efficient lockout queries: recent failures per user
        builder.HasIndex(e => new { e.UserId, e.AttemptedAt })
               .HasDatabaseName("IX_reauth_attempts_user_id_attempted_at")
               .IsDescending(false, true);
    }
}
