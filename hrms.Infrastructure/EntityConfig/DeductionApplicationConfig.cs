using Hrms.Domain;
using Hrms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hrms.Infrastructure.EntityConfig;

public class DeductionApplicationConfig : IEntityTypeConfiguration<DeductionApplication>
{
    public void Configure(EntityTypeBuilder<DeductionApplication> builder)
    {
        builder.Property(x => x.FrequencyOfPayment)
          .HasConversion(
                v => v.ToString(),
                v => EnumParserConfig.SafeParseEnum(v, DeductionFrequency.SemiMonthly)
            );
    }
}
