using Hrms.Domain;
using Hrms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hrms.Infrastructure.EntityConfig
{
    public class OtherIncomeConfig : IEntityTypeConfiguration<OtherIncome>
    {
        public void Configure(EntityTypeBuilder<OtherIncome> builder)
        {
            builder.Property(x => x.IncomeClass)
                  .HasConversion(
                        v => v.ToString(),
                        v => EnumParserConfig.SafeParseEnum(v, IncomeClassType.Others)
                    );

        }
    }
}
