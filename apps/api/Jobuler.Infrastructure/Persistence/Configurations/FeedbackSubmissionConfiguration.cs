using Jobuler.Domain.Feedback;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jobuler.Infrastructure.Persistence.Configurations;

public class FeedbackSubmissionConfiguration : IEntityTypeConfiguration<FeedbackSubmission>
{
    public void Configure(EntityTypeBuilder<FeedbackSubmission> builder)
    {
        builder.ToTable("feedback_submissions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.SubmittedAtUtc).HasColumnName("submitted_at_utc").IsRequired();
        builder.HasIndex(x => new { x.UserId, x.SubmittedAtUtc });
    }
}
