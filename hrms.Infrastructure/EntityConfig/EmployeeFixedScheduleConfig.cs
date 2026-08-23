using Hrms.Domain.Entities.EmployeeEntities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hrms.Infrastructure.EntityConfig;

public class EmployeeFixedScheduleConfig : IEntityTypeConfiguration<EmployeeFixedSchedule>
{
    public void Configure(EntityTypeBuilder<EmployeeFixedSchedule> builder)
    {
        builder.HasOne(x => x.Employee)
            .WithMany(e => e.FixedSchedule)
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.TimeShift)
            .WithMany()
            .HasForeignKey(x => x.TimeShiftId)
            .OnDelete(DeleteBehavior.Restrict);

        // One shift per employee per day of week.
        builder.HasIndex(x => new { x.EmployeeId, x.DayName }).IsUnique();
    }
}
