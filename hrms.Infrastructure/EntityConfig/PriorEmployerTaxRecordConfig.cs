using Hrms.Domain.Entities.EmployeeEntities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hrms.Infrastructure.EntityConfig;

public class PriorEmployerTaxRecordConfig : IEntityTypeConfiguration<PriorEmployerTaxRecord>
{
    public void Configure(EntityTypeBuilder<PriorEmployerTaxRecord> builder)
    {
        // One record per employee per calendar year — matches the LeaveCredits convention for
        // a year-scoped child record.
        builder.HasIndex(x => new { x.EmployeeId, x.Year }).IsUnique();

        builder.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
    }
}
