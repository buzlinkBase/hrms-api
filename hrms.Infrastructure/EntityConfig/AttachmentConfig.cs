using Hrms.Domain.Entities.Attachments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hrms.Infrastructure.EntityConfig;

public class AttachmentConfig : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        UtcTimestamps.Apply(builder);
        builder.Property(x => x.OwnerType).HasMaxLength(30).HasConversion<string>();
        builder.Property(x => x.StorageKey).HasMaxLength(512).IsRequired();
        builder.Property(x => x.FileName).HasMaxLength(255).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(100).IsRequired();

        // A record's files, and the "how many files does it have" count.
        builder.HasIndex(x => new { x.OwnerType, x.OwnerId });
    }
}

public class AttachmentSettingConfig : IEntityTypeConfiguration<AttachmentSetting>
{
    public void Configure(EntityTypeBuilder<AttachmentSetting> builder)
    {
        UtcTimestamps.Apply(builder);
        builder.Property(x => x.OwnerType).HasMaxLength(30).HasConversion<string>();
        // One switch per feature per company.
        builder.HasIndex(x => new { x.TenantId, x.OwnerType }).IsUnique();
    }
}
