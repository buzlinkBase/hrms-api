using Hrms.Domain;
using Hrms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hrms.Infrastructure.EntityConfig;

public class OtherIncomeApplicationConfig : IEntityTypeConfiguration<OtherIncomeApplication>
{
    public void Configure(EntityTypeBuilder<OtherIncomeApplication> builder)
    {
        builder.Property(x => x.FrequencyOfPayment)
          .HasConversion(
                v => v.ToString(),
                v => EnumParserConfig.SafeParseEnum(v, AllowanceFrequency.SemiMonthly)
            );
    }
}
