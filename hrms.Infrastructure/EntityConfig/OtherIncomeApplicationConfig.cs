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

        builder.Property(x => x.PayMode)
            .HasConversion(
                v => v.ToString(),
                v => EnumParserConfig.SafeParseEnum(v, OtherIncomePayMode.FixedSchedule)
            );
        builder.Property(x => x.AttendanceBasis)
            .HasConversion(
                v => v.ToString(),
                v => EnumParserConfig.SafeParseEnum(v, OtherIncomeAttendanceBasis.None)
            );

        // One FK only. Without this, EF invented a shadow OtherIncomeApplicationId next to the
        // plain ApplicationId column and the two drifted apart. Restrict: rows are deleted
        // explicitly by IncomeAplMainService and the parent is only ever soft-deleted. No back
        // navigation: PayrollsController returns raw schedule rows, and a lazy parent would be
        // serialized along with them.
        builder.HasMany(x => x.Schedule)
            .WithOne()
            .HasForeignKey(x => x.OtherIncomeApplicationId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
    }
}
