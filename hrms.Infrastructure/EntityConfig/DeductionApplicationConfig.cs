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

        // One FK only (same story as OtherIncomeApplicationConfig): the plain ApplicationId column
        // and EF's shadow DeductionApplicationId drifted apart. Restrict: installments are deleted
        // explicitly by DeductionApplicationService and the loan is only ever soft-deleted.
        builder.HasMany(x => x.Breakdown)
            .WithOne()
            .HasForeignKey(x => x.DeductionApplicationId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
    }
}
