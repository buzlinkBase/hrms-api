using Hrms.Domain;
using Hrms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hrms.Infrastructure.EntityConfig
{
    public class LeaveEfConfig : IEntityTypeConfiguration<Leave>
    {
        public void Configure(EntityTypeBuilder<Leave> builder)
        {

            builder.Property(x => x.PaySource)
            .HasConversion(
                    v => v.ToString(),
                    v => EnumParserConfig.SafeParseEnum(v, PaySource.Unpaid)
                );


            builder.HasData(
         new Leave
         {
             Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
             TenantId = Guid.Parse("C1B8AAAF-6BFF-4F68-97C7-626F16EA9197"),
             Code = "SIL",
             Description = "Service Incentive Leave",
             Remarks = "Labor Code mandated",
             Credits = 5,
             PaySource = PaySource.Company,
             LeaveReset = LeaveReset.PerPeriod
         },
        new Leave
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            TenantId = Guid.Parse("C1B8AAAF-6BFF-4F68-97C7-626F16EA9197"),
            Code = "ML",
            Description = "Maternity Leave",
            Remarks = "RA 11210 Expanded Maternity Leave Law",
            Credits = 105,
            PaySource = PaySource.Government, // SSS/private sector or Employer/public sector
            LeaveReset = LeaveReset.PerEvent
        },
        new Leave
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            TenantId = Guid.Parse("C1B8AAAF-6BFF-4F68-97C7-626F16EA9197"),
            Code = "PL",
            Description = "Paternity Leave",
            Remarks = "RA 8187",
            Credits = 7,
            PaySource = PaySource.Company,
            LeaveReset = LeaveReset.PerEvent
        },
        new Leave
        {
            Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
            TenantId = Guid.Parse("C1B8AAAF-6BFF-4F68-97C7-626F16EA9197"),
            Code = "SPL",
            Description = "Parental Leave for Solo Parents",
            Remarks = "RA 8972 Solo Parents’ Welfare Act",
            Credits = 7,
            PaySource = PaySource.Company,
            LeaveReset = LeaveReset.PerPeriod
        },
        new Leave
        {
            Id = Guid.Parse("55555555-5555-5555-5555-555555555555"),
            TenantId = Guid.Parse("C1B8AAAF-6BFF-4F68-97C7-626F16EA9197"),
            Code = "SLW",
            Description = "Special Leave for Women (Gynecological Disorders)",
            Remarks = "RA 9710 Magna Carta of Women",
            Credits = 60,
            PaySource = PaySource.Company,
            LeaveReset = LeaveReset.PerEvent
        },
        new Leave
        {
            Id = Guid.Parse("66666666-6666-6666-6666-666666666666"),
            TenantId = Guid.Parse("C1B8AAAF-6BFF-4F68-97C7-626F16EA9197"),
            Code = "VAWC",
            Description = "Leave for Victims of Violence Against Women and Children",
            Remarks = "RA 9262",
            Credits = 10,
            PaySource = PaySource.Company,
            LeaveReset = LeaveReset.PerEvent
        },
        new Leave
        {
            Id = Guid.Parse("77777777-7777-7777-7777-777777777777"),
            TenantId = Guid.Parse("C1B8AAAF-6BFF-4F68-97C7-626F16EA9197"),
            Code = "MC",
            Description = "Magna Carta Leave (Gov’t Employees)",
            Remarks = "RA 7305 for Public Health Workers",
            Credits = 5,
            PaySource = PaySource.Company,
            LeaveReset = LeaveReset.PerPeriod
        },
        new Leave
        {
            Id = Guid.Parse("88888888-8888-8888-8888-888888888888"),
            TenantId = Guid.Parse("C1B8AAAF-6BFF-4F68-97C7-626F16EA9197"),
            Code = "RL",
            Description = "Rehabilitation Leave (Occupational Injuries)",
            Remarks = "Occupational safety provisions",
            Credits = 120,
            PaySource = PaySource.Company,
            LeaveReset = LeaveReset.PerEvent
        },
        new Leave
        {
            Id = Guid.Parse("99999999-9999-9999-9999-999999999999"),
            TenantId = Guid.Parse("C1B8AAAF-6BFF-4F68-97C7-626F16EA9197"),
            Code = "EL",
            Description = "Educational Leave (Gov’t Employees)",
            Remarks = "Civil Service rules",
            Credits = 10,
            PaySource = PaySource.Company,
            LeaveReset = LeaveReset.PerPeriod
        },
        new Leave
        {
            Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            TenantId = Guid.Parse("C1B8AAAF-6BFF-4F68-97C7-626F16EA9197"),
            Code = "SEL",
            Description = "Special Emergency Leave (Calamities)",
            Remarks = "Company policy / calamity provisions",
            Credits = 5,
            PaySource = PaySource.Company,
            LeaveReset = LeaveReset.PerEvent
        },
        new Leave
        {
            Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            TenantId = Guid.Parse("C1B8AAAF-6BFF-4F68-97C7-626F16EA9197"),
            Code = "VL",
            Description = "Vacation Leave",
            Remarks = "Company policy benefit",
            Credits = 10,
            PaySource = PaySource.Company,
            LeaveReset = LeaveReset.PerPeriod
        },
        new Leave
        {
            Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            TenantId = Guid.Parse("C1B8AAAF-6BFF-4F68-97C7-626F16EA9197"),
            Code = "SL",
            Description = "Sick Leave",
            Remarks = "Company policy benefit",
            Credits = 10,
            PaySource = PaySource.Company,
            LeaveReset = LeaveReset.PerPeriod
        });
        }
    }
}
