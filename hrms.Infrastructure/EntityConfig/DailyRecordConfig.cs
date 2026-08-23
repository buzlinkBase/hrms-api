using Hrms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;


namespace Hrms.Infrastructure.EntityConfig;

public class DailyRecordConfig : IEntityTypeConfiguration<DailyRecord>
{
    public void Configure(EntityTypeBuilder<DailyRecord> builder)
    {
        //builder.Property(x => x.RecordStatus)
        //  .HasConversion(
        //        v => v.ToString(),
        //        v => EnumParserConfig.SafeParseEnum(v, DTRStatus.LOCKED)
        //    );

        //builder.Property(x => x.Source)
        //  .HasConversion(
        //        v => v.ToString(),
        //        v => EnumParserConfig.SafeParseEnum(v, DTRSOURCE.SYSTEMCALC)
        //    );

        builder.HasOne(d => d.Employee)
                    .WithMany()
                    .HasForeignKey(d => d.EmployeeId)
                    .IsRequired();

        builder.HasMany(d => d.LeavesInfo)
            .WithOne(x => x.DTR)
            .HasForeignKey(d => d.DTRId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
    }
}
