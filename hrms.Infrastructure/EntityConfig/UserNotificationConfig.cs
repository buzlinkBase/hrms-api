using Hrms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hrms.Infrastructure.EntityConfig;

public class UserNotificationConfig : IEntityTypeConfiguration<UserNotification>
{
    public void Configure(EntityTypeBuilder<UserNotification> builder)
    {
        UtcTimestamps.Apply(builder);
        builder.Property(x => x.RecipientEmail).HasMaxLength(256);
        builder.Property(x => x.Type).HasMaxLength(50);
        builder.Property(x => x.ApplicationTypeLabel).HasMaxLength(100);
        builder.Property(x => x.ApplicantName).HasMaxLength(200);
        builder.Property(x => x.StatusLabel).HasMaxLength(100);
        builder.Property(x => x.Note).HasMaxLength(1000);

        // A person's bell (newest first) and unread count, by account id or login email.
        builder.HasIndex(x => new { x.RecipientUserId, x.ReadAt, x.CreatedAt });
        builder.HasIndex(x => new { x.RecipientEmail, x.ReadAt, x.CreatedAt });
        // The retention job's sweep.
        builder.HasIndex(x => x.CreatedAt);
    }
}
