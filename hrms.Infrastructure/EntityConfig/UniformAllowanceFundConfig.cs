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
    }
}
