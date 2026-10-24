using Hrms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hrms.Infrastructure.EntityConfig;

public class WorkSchedulePlanConfig : IEntityTypeConfiguration<WorkSchedulePlan>
{
    public void Configure(EntityTypeBuilder<WorkSchedulePlan> builder)
    {
        // WorkSchedulePlanService.GetAllCustomShiftsAync filters by PayrollDate range (and the
        // GroupBy keys off EmployeeId+PayrollDate) on every DTR run and roster report call.
        builder.HasIndex(x => new { x.EmployeeId, x.PayrollDate });

        // Removing a project site just untags the days scheduled there.
        builder.HasOne(x => x.OperationArea)
            .WithMany()
            .HasForeignKey(x => x.OperationAreaId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
