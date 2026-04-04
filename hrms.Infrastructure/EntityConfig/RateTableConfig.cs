using Hrms.Domain;
using Hrms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hrms.Infrastructure.EntityConfig;

public class RateTableConfig : IEntityTypeConfiguration<RateTable>
{
    public void Configure(EntityTypeBuilder<RateTable> builder)
    {
        builder.Property(x => x.Type)
            .HasConversion(
                v => v.ToString(),
                v => EnumParserConfig.SafeParseEnum(v, RateType.REGULAR)
            );


    //    builder.HasData(
    //    new RateTable
    //    {
    //        Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
    //        TenantId = Guid.Parse("C1B8AAAF-6BFF-4F68-97C7-626F16EA9197"),
    //        Type = RateType.REGULAR,
    //        ShortDescription = "REG",
    //        Description = "Regular Day",
    //        Rate = 1.00m
    //    },
    //    new RateTable
    //    {
    //        Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
    //        TenantId = Guid.Parse("C1B8AAAF-6BFF-4F68-97C7-626F16EA9197"),
    //        Type = RateType.NIGHTDIFF,
    //        ShortDescription = "ND",
    //        Description = "Night Differential",
    //        Rate = 1.10m
    //    },
    //    new RateTable
    //    {
    //        Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
    //        TenantId = Guid.Parse("C1B8AAAF-6BFF-4F68-97C7-626F16EA9197"),
    //        Type = RateType.OVERTIME,
    //        ShortDescription = "OT",
    //        Description = "Overtime",
    //        Rate = 1.25m
    //    },
    //    new RateTable
    //    {
    //        Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
    //        TenantId = Guid.Parse("C1B8AAAF-6BFF-4F68-97C7-626F16EA9197"),
    //        Type = RateType.RESTDAY_DUTY,
    //        ShortDescription = "RD",
    //        Description = "Rest Day Duty",
    //        Rate = 1.30m
    //    },
    //    new RateTable
    //    {
    //        Id = Guid.Parse("55555555-5555-5555-5555-555555555555"),
    //        TenantId = Guid.Parse("C1B8AAAF-6BFF-4F68-97C7-626F16EA9197"),
    //        Type = RateType.LEGAL_HOLIDAY,
    //        ShortDescription = "LH",
    //        Description = "Legal Holiday (No Work)",
    //        Rate = 1.00m
    //    },
    //    new RateTable
    //    {
    //        Id = Guid.Parse("66666666-6666-6666-6666-666666666666"),
    //        TenantId = Guid.Parse("C1B8AAAF-6BFF-4F68-97C7-626F16EA9197"),
    //        Type = RateType.LEGAL_HOLIDAY_DUTY,
    //        ShortDescription = "LH-DUTY",
    //        Description = "Legal Holiday Duty",
    //        Rate = 2.00m
    //    },
    //    new RateTable
    //    {
    //        Id = Guid.Parse("77777777-7777-7777-7777-777777777777"),
    //        TenantId = Guid.Parse("C1B8AAAF-6BFF-4F68-97C7-626F16EA9197"),
    //        Type = RateType.SPECIAL_WORKING,
    //        ShortDescription = "SP-WH",
    //        Description = "Special Working Holiday",
    //        Rate = 1.00m
    //    },
    //    new RateTable
    //    {
    //        Id = Guid.Parse("88888888-8888-8888-8888-888888888888"),
    //        TenantId = Guid.Parse("C1B8AAAF-6BFF-4F68-97C7-626F16EA9197"),
    //        Type = RateType.SPECIAL_NON_WORKING,
    //        ShortDescription = "SP-NWH",
    //        Description = "Special Non-Working Holiday",
    //        Rate = 1.30m
    //    },
    //    new RateTable
    //    {
    //        Id = Guid.Parse("99999999-9999-9999-9999-999999999999"),
    //        TenantId = Guid.Parse("C1B8AAAF-6BFF-4F68-97C7-626F16EA9197"),
    //        Type = RateType.RESTDAY_SPECIAL,
    //        ShortDescription = "RD-SP",
    //        Description = "Special Rest Day",
    //        Rate = 1.50m
    //    }
    //);

    }
}
