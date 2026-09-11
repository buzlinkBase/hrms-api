using Hrms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hrms.Infrastructure.EntityConfig;

public class ChangeRestDayConfig : IEntityTypeConfiguration<ChangeRestDay>
{
    public void Configure(EntityTypeBuilder<ChangeRestDay> builder)
    {
        // ChangeRestDayService.GetChangeRestDays filters by EmployeeId set + PayrollDate range
        // on every DTR run and roster report call. (Employee FK relationship is left to EF's
        // default convention -- not redefined here, to avoid changing delete behavior.)
        builder.HasIndex(x => new { x.EmployeeId, x.PayrollDate });
    }
}
