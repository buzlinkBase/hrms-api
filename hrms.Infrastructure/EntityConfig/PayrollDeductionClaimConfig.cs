using Hrms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hrms.Infrastructure.EntityConfig;

public class PayrollDeductionClaimConfig : IEntityTypeConfiguration<PayrollDeductionClaim>
{
    public void Configure(EntityTypeBuilder<PayrollDeductionClaim> builder)
    {
        // One carrier run per employee per deduction period. This index, not application code, is
        // what stops two runs generated at the same moment from both deducting.
        builder.HasIndex(x => new { x.EmployeeId, x.PeriodStart }).IsUnique();
        // Releasing a deleted run's claims.
        builder.HasIndex(x => x.PayrollBatchId);

        builder.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
    }
}
