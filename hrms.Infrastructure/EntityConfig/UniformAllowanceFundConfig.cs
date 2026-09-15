using Hrms.Domain;
using Hrms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hrms.Infrastructure.EntityConfig;

public class UniformAllowanceFundConfig : IEntityTypeConfiguration<UniformAllowanceFund>
{
    public void Configure(EntityTypeBuilder<UniformAllowanceFund> builder)
    {
        builder.HasIndex(x => x.EmployeeId).IsUnique();
    }
}

public class UniformAllowanceLedgerConfig : IEntityTypeConfiguration<UniformAllowanceLedger>
{
    public void Configure(EntityTypeBuilder<UniformAllowanceLedger> builder)
    {
        builder.Property(x => x.EntryType)
            .HasConversion(
                v => v.ToString(),
                v => EnumParserConfig.SafeParseEnum(v, UniformAllowanceEntryType.Adjustment));

        builder.Property(x => x.AccrualDedupeKey).HasMaxLength(100);
        // Null for every non-Accrual entry -- multiple nulls never collide in a unique index,
        // so this only actually constrains Accrual rows. See UniformAllowanceAccrualWorker.
        builder.HasIndex(x => x.AccrualDedupeKey).IsUnique();
    }
}
