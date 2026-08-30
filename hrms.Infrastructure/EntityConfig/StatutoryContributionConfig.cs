using Hrms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hrms.Infrastructure.EntityConfig;

// These 4 tables can reach millions of rows over time (one row per employee per statutory
// type per payroll run). Neither of their real query paths — deleting a whole batch's rows,
// or reading an employee's contributions-to-date for balance-netting — had any supporting
// index before this; both were previously unindexed scans within the tenant partition.

internal class SSSContributionConfig : IEntityTypeConfiguration<SSSContribution>
{
    public void Configure(EntityTypeBuilder<SSSContribution> builder)
    {
        builder.HasIndex(x => x.PayrollBatchId);
        builder.HasIndex(x => new { x.EmployeeId, x.PayrollDate });
    }
}

internal class PHICContributionConfig : IEntityTypeConfiguration<PHICContribution>
{
    public void Configure(EntityTypeBuilder<PHICContribution> builder)
    {
        builder.HasIndex(x => x.PayrollBatchId);
        builder.HasIndex(x => new { x.EmployeeId, x.PayrollDate });
    }
}

internal class HDMFContributionConfig : IEntityTypeConfiguration<HDMFContribution>
{
    public void Configure(EntityTypeBuilder<HDMFContribution> builder)
    {
        builder.HasIndex(x => x.PayrollBatchId);
        builder.HasIndex(x => new { x.EmployeeId, x.PayrollDate });
    }
}

internal class WTaxContributionConfig : IEntityTypeConfiguration<WTaxContribution>
{
    public void Configure(EntityTypeBuilder<WTaxContribution> builder)
    {
        builder.HasIndex(x => x.PayrollBatchId);
        builder.HasIndex(x => new { x.EmployeeId, x.PayrollDate });
    }
}
