using Hrms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hrms.Infrastructure.EntityConfig;

public class PayrollGroupConfig : IEntityTypeConfiguration<PayrollGroup>
{
    public void Configure(EntityTypeBuilder<PayrollGroup> builder)
    {
        //builder.HasData(
        //    new PayrollGroup
        //    {
        //        Id = Guid.Parse("398BBB60-4A8F-4095-A478-59B4F4E6A22F"), // PayrollGroup ID
        //        TenantId = Guid.Parse("C1B8AAAF-6BFF-4F68-97C7-626F16EA9197"),
        //        Code = "SM01",
        //        Name = "Semi-Monthly",
        //        PayrollFrequency = PayrollFrequency.SEMI_MONTHLY,
        //    });
    }
}

public class CutoffConfig : IEntityTypeConfiguration<CutoffDay>
{
    public void Configure(EntityTypeBuilder<CutoffDay> builder)
    {
        builder.HasOne(x => x.PayrollGroup)
            .WithMany(pg => pg.CutoffDays) // better to explicitly map navigation
            .HasForeignKey(x => x.PayrollGroupId)
            .OnDelete(DeleteBehavior.Cascade);

        //builder.HasData(
        //    new CutoffDay
        //    {
        //        Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), // unique ID
        //        TenantId = Guid.Parse("C1B8AAAF-6BFF-4F68-97C7-626F16EA9197"),
        //        PayrollGroupId = Guid.Parse("398BBB60-4A8F-4095-A478-59B4F4E6A22F"), // FK to PayrollGroup
        //        Day = 15,
        //        Label = "First Cutoff",
        //        IsEndOfMonth = false
        //    },
        //    new CutoffDay
        //    {
        //        Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), // another unique ID
        //        TenantId = Guid.Parse("C1B8AAAF-6BFF-4F68-97C7-626F16EA9197"),
        //        PayrollGroupId = Guid.Parse("398BBB60-4A8F-4095-A478-59B4F4E6A22F"), // FK to PayrollGroup
        //        Day = 31,
        //        Label = "Second Cutoff",
        //        IsEndOfMonth = true
        //    });
    }
}