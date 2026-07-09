using Hrms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hrms.Infrastructure.EntityConfig;

internal class AttendanceConfig : IEntityTypeConfiguration<Attendance>
{
    public void Configure(EntityTypeBuilder<Attendance> builder)
    {
        builder.Property(e => e.Boundary)
        .HasColumnType("geometry")
        .HasAnnotation("MySql:SpatialReferenceSystemId", 4326);

        builder.HasIndex(e => new { e.LogSource, e.BatchCode })
            .HasDatabaseName("IX_Attendance_LogSource_BatchCode");

        builder.HasIndex(e => new { e.LogSource, e.WorkDateTime }) 
         .HasDatabaseName("IX_Attendance_ls_wt");

        builder.HasIndex(e => new { e.BranchId, e.DepartmentId, e.OperationAreaId, e.ClientId })
        .HasDatabaseName("IX_Att_BRId_DepId_Area_ClId_LS");

    }
}
