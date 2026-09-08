using Hrms.Domain.Entities.EmployeeEntities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hrms.Infrastructure.EntityConfig;

public class PayrollOpeningBalanceConfig : IEntityTypeConfiguration<PayrollOpeningBalance>
{
    public void Configure(EntityTypeBuilder<PayrollOpeningBalance> builder)
    {
        // One opening balance per employee per calendar year — same convention as
        // PriorEmployerTaxRecord/LeaveCredits.
        builder.HasIndex(x => new { x.EmployeeId, x.Year }).IsUnique();

        builder.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
    }
}
