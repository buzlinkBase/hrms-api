using Hrms.Domain;
using Hrms.Domain.Entities.Announcements;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Hrms.Infrastructure.EntityConfig;

// Every announcement timestamp is written as UTC, but the database hands it back with an
// unspecified Kind -- serialized without a "Z", the browser would read it as local time (8 hours
// off in Manila). Marking it UTC on the way out makes the JSON say so.
internal static class UtcTimestamps
{
    private static readonly ValueConverter<DateTime, DateTime> Utc =
        new(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
    private static readonly ValueConverter<DateTime?, DateTime?> NullableUtc =
        new(v => v, v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

    public static void Apply<T>(EntityTypeBuilder<T> builder) where T : class
    {
        foreach (var property in builder.Metadata.GetProperties())
        {
            if (property.ClrType == typeof(DateTime)) property.SetValueConverter(Utc);
            else if (property.ClrType == typeof(DateTime?)) property.SetValueConverter(NullableUtc);
        }
    }
}

public class AnnouncementConfig : IEntityTypeConfiguration<Announcement>
{
    public void Configure(EntityTypeBuilder<Announcement> builder)
    {
        UtcTimestamps.Apply(builder);
        builder.Property(x => x.Title).HasMaxLength(200);
        builder.Property(x => x.CreatedByName).HasMaxLength(200);
        builder.Property(x => x.PublishedByName).HasMaxLength(200);
        builder.Property(x => x.UnpublishedByName).HasMaxLength(200);
        builder.Property(x => x.UnpublishReason).HasMaxLength(500);

        builder.Property(x => x.Priority)
            .HasMaxLength(20)
            .HasConversion(v => v.ToString(), v => EnumParserConfig.SafeParseEnum(v, AnnouncementPriority.Normal));
        builder.Property(x => x.State)
            .HasMaxLength(20)
            .HasConversion(v => v.ToString(), v => EnumParserConfig.SafeParseEnum(v, AnnouncementState.Draft));
        builder.Property(x => x.StateBeforeArchive)
            .HasMaxLength(20)
            .HasConversion(
                v => v.HasValue ? v.Value.ToString() : null,
                v => v == null ? null : EnumParserConfig.SafeParseEnum(v, AnnouncementState.Published));
        builder.Property(x => x.StateBeforeDelete)
            .HasMaxLength(20)
            .HasConversion(
                v => v.HasValue ? v.Value.ToString() : null,
                v => v == null ? null : EnumParserConfig.SafeParseEnum(v, AnnouncementState.Published));

        // AnnouncementLifecycleScheduler scans these every minute, across companies.
        builder.HasIndex(x => new { x.State, x.PublishAtUtc });
        builder.HasIndex(x => new { x.State, x.ExpiresAtUtc });
        builder.HasIndex(x => new { x.State, x.BinnedAtUtc });

        builder.HasMany(x => x.AudienceRules).WithOne(x => x.Announcement)
            .HasForeignKey(x => x.AnnouncementId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Recipients).WithOne(x => x.Announcement)
            .HasForeignKey(x => x.AnnouncementId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class AnnouncementAudienceRuleConfig : IEntityTypeConfiguration<AnnouncementAudienceRule>
{
    public void Configure(EntityTypeBuilder<AnnouncementAudienceRule> builder)
    {
        builder.Property(x => x.Kind)
            .HasMaxLength(30)
            .HasConversion(v => v.ToString(), v => EnumParserConfig.SafeParseEnum(v, AudienceRuleKind.Employee));
        builder.Property(x => x.TargetValue).HasMaxLength(100);
        builder.Property(x => x.TargetLabel).HasMaxLength(200);
    }
}

public class AnnouncementRecipientConfig : IEntityTypeConfiguration<AnnouncementRecipient>
{
    public void Configure(EntityTypeBuilder<AnnouncementRecipient> builder)
    {
        UtcTimestamps.Apply(builder);
        builder.Property(x => x.Email).HasMaxLength(256);
        builder.Property(x => x.EmployeeNo).HasMaxLength(50);
        builder.Property(x => x.EmployeeName).HasMaxLength(200);
        builder.Property(x => x.DepartmentName).HasMaxLength(200);
        builder.Property(x => x.PositionName).HasMaxLength(200);
        builder.Property(x => x.BranchName).HasMaxLength(200);

        // One copy per employee per announcement -- also what makes a racing double publish safe.
        builder.HasIndex(x => new { x.AnnouncementId, x.EmployeeId }).IsUnique();
        // The employee's own list and unread count.
        builder.HasIndex(x => new { x.EmployeeId, x.FirstViewedAt });
    }
}
