using Hrms.Domain;
using Hrms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hrms.Infrastructure.EntityConfig;

public class RetirementFundConfig : IEntityTypeConfiguration<RetirementFund>
{
    public void Configure(EntityTypeBuilder<RetirementFund> builder)
    {
        builder.HasIndex(x => x.EmployeeId).IsUnique();
    }
}

public class RetirementLedgerConfig : IEntityTypeConfiguration<RetirementLedger>
{
    public void Configure(EntityTypeBuilder<RetirementLedger> builder)
    {
        builder.Property(x => x.EntryType)
            .HasConversion(
                v => v.ToString(),
                v => EnumParserConfig.SafeParseEnum(v, RetirementLedgerEntryType.Adjustment));
    }
}
