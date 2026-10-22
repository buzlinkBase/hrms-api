using Hrms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hrms.Infrastructure.EntityConfig;

internal class AttendanceOriginalLogConfig : IEntityTypeConfiguration<AttendanceOriginalLog>
{
    public void Configure(EntityTypeBuilder<AttendanceOriginalLog> builder)
    {
        builder.Property(e => e.Boundary)
            .HasColumnType("geometry")
            .HasAnnotation("MySql:SpatialReferenceSystemId", 4326);

        // Only ActedAt is UTC -- WorkDateTime is the punch's local wall time, like Attendance's.
        // Read back with an unspecified Kind it would serialize without a "Z" (8 hours off).
        builder.Property(e => e.ActedAt)
            .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        // The re-upload/re-sync skip on every insert path (AttendanceService.AddRangeAsync).
        builder.HasIndex(e => new { e.BioId, e.WorkDateTime })
            .HasDatabaseName("IX_AttendanceOriginalLog_BioId_WorkDateTime");
        // An edited log's saved original.
        builder.HasIndex(e => e.AttendanceId)
            .HasDatabaseName("IX_AttendanceOriginalLog_AttendanceId");
        // Upload batch list and batch delete.
        builder.HasIndex(e => new { e.BatchCode, e.LogSource })
            .HasDatabaseName("IX_AttendanceOriginalLog_BatchCode_LogSource");
    }
}
