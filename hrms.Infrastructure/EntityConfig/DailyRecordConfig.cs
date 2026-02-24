using Hrms.Domain;
using Hrms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hrms.Infrastructure.EntityConfig;

public class DailyRecordConfig : IEntityTypeConfiguration<DailyRecord>
{
    public void Configure(EntityTypeBuilder<DailyRecord> builder)
    {
        builder.Property(x => x.RecordStatus)
          .HasConversion(
                v => v.ToString(),
                v => EnumParserConfig.SafeParseEnum(v, DTRStatus.LOCKED)
            );

        builder.Property(x => x.Source)
          .HasConversion(
                v => v.ToString(),
                v => EnumParserConfig.SafeParseEnum(v, DTRSOURCE.SYSTEMCALC)
            );

        builder
        .HasOne(d => d.Employee)
        .WithMany()
        .HasForeignKey(d => d.EmployeeId)
        .IsRequired(false);

        //builder.HasData(
        //    new DailyRecord
        //    {
        //        Id = Guid.Parse("398BBB60-4A8F-4095-A478-59B4F4E6A22F"),
        //        TenantId = Guid.Parse("C1B8AAAF-6BFF-4F68-97C7-626F16EA9197"),
        //        WorkDate = new DateOnly(2025, 12, 15),
        //        EmployeeId = Guid.Parse("398BBB60-4A8F-4095-A478-59B4F4E6A22F"),
        //        BioId = 01,
        //        FullName = "jhoncee",
        //        RegularNetHours = 8,
        //        RegDayOTMinutes = 120,
        //        RecordStatus = DTRStatus.LOCKED,
        //        WorkType = "Regular Work Day",
        //    });
    }
}
