using Hrms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hrms.Infrastructure.EntityConfig;

internal class SSSTableConfig : IEntityTypeConfiguration<SSSTable>
{
    public void Configure(EntityTypeBuilder<SSSTable> builder)
    {
      //  builder.HasData(
      //new SSSTable
      //{
      //    Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
      //    TenantId = Guid.Parse("C1B8AAAF-6BFF-4F68-97C7-626F16EA9197"),
      //    EffectiveDate = new DateOnly(2025, 1, 1),
      //    RangeFrom = 0,
      //    RangeTo = 4_999,
      //    MSC = 5_000,
      //    EE = 750,   // Employee share (15% of MSC split)
      //    ER = 875,   // Employer share
      //    EC = 10     // EC (fixed)
      //},
      //new SSSTable
      //{
      //    Id = Guid.Parse("11111111-1111-1111-1111-111111111112"),
      //    TenantId = Guid.Parse("C1B8AAAF-6BFF-4F68-97C7-626F16EA9197"),
      //    EffectiveDate = new DateOnly(2025, 1, 1),
      //    RangeFrom = 5_000,
      //    RangeTo = 9_999,
      //    MSC = 10_000,
      //    EE = 1_500,
      //    ER = 1_750,
      //    EC = 10
      //},
      //new SSSTable
      //{
      //    Id = Guid.Parse("11111111-1111-1111-1111-111111111113"),
      //    TenantId = Guid.Parse("C1B8AAAF-6BFF-4F68-97C7-626F16EA9197"),
      //    EffectiveDate = new DateOnly(2025, 1, 1),
      //    RangeFrom = 10_000,
      //    RangeTo = 14_999,
      //    MSC = 15_000,
      //    EE = 2_250,
      //    ER = 2_625,
      //    EC = 10
      //},
      //new SSSTable
      //{
      //    Id = Guid.Parse("11111111-1111-1111-1111-111111111114"),
      //    TenantId = Guid.Parse("C1B8AAAF-6BFF-4F68-97C7-626F16EA9197"),
      //    EffectiveDate = new DateOnly(2025, 1, 1),
      //    RangeFrom = 15_000,
      //    RangeTo = 19_999,
      //    MSC = 20_000,
      //    EE = 3_000,
      //    ER = 3_500,
      //    EC = 10
      //},
      //new SSSTable
      //{
      //    Id = Guid.Parse("11111111-1111-1111-1111-111111111115"),
      //    TenantId = Guid.Parse("C1B8AAAF-6BFF-4F68-97C7-626F16EA9197"),
      //    EffectiveDate = new DateOnly(2025, 1, 1),
      //    RangeFrom = 20_000,
      //    RangeTo = 24_999,
      //    MSC = 25_000,
      //    EE = 3_750,
      //    ER = 4_375,
      //    EC = 10
      //},
      //new SSSTable
      //{
      //    Id = Guid.Parse("11111111-1111-1111-1111-111111111116"),
      //    TenantId = Guid.Parse("C1B8AAAF-6BFF-4F68-97C7-626F16EA9197"),
      //    EffectiveDate = new DateOnly(2025, 1, 1),
      //    RangeFrom = 25_000,
      //    RangeTo = 29_999,
      //    MSC = 30_000,
      //    EE = 4_500,
      //    ER = 5_250,
      //    EC = 10
      //},
      //new SSSTable
      //{
      //    Id = Guid.Parse("11111111-1111-1111-1111-111111111117"),
      //    TenantId = Guid.Parse("C1B8AAAF-6BFF-4F68-97C7-626F16EA9197"),
      //    EffectiveDate = new DateOnly(2025, 1, 1),
      //    RangeFrom = 30_000,
      //    RangeTo = 9_999_999,
      //    MSC = 35_000,
      //    EE = 5_250,
      //    ER = 6_125,
      //    EC = 10
      //});
    }
}
internal class PHICTableConfig : IEntityTypeConfiguration<PHICTable>
{
    public void Configure(EntityTypeBuilder<PHICTable> builder)
    {
        //builder.HasData(
        //    new PHICTable
        //    {
        //        Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        //        TenantId = Guid.Parse("C1B8AAAF-6BFF-4F68-97C7-626F16EA9197"),
        //        EffectiveDate = new DateOnly(2025, 1, 1),
        //        PremiumRate = 0.10m,
        //        MinSalaryBase = 1500m,
        //        MaxSalaryBase = 2000m,
        //        EmployeeShare = 100m,
        //        EmployerShare = 200m,
        //        TotalContribution = 300m
        //    },
        //    new PHICTable
        //    {
        //        Id = Guid.Parse("11111111-1111-1111-1111-111111111112"),
        //        TenantId = Guid.Parse("C1B8AAAF-6BFF-4F68-97C7-626F16EA9197"),
        //        EffectiveDate = new DateOnly(2025, 1, 1),
        //        PremiumRate = 0.10m,
        //        MinSalaryBase = 2001m,
        //        MaxSalaryBase = 3000m,
        //        EmployeeShare = 150m,
        //        EmployerShare = 300m,
        //        TotalContribution = 450m
        //    },
        //    new PHICTable
        //    {
        //        Id = Guid.Parse("11111111-1111-1111-1111-111111111113"),
        //        TenantId = Guid.Parse("C1B8AAAF-6BFF-4F68-97C7-626F16EA9197"),
        //        EffectiveDate = new DateOnly(2025, 1, 1),
        //        PremiumRate = 0.10m,
        //        MinSalaryBase = 3001m,
        //        MaxSalaryBase = 4000m,
        //        EmployeeShare = 200m,
        //        EmployerShare = 400m,
        //        TotalContribution = 600m
        //    },
        //    new PHICTable
        //    {
        //        Id = Guid.Parse("11111111-1111-1111-1111-111111111114"),
        //        TenantId = Guid.Parse("C1B8AAAF-6BFF-4F68-97C7-626F16EA9197"),
        //        EffectiveDate = new DateOnly(2025, 1, 1),
        //        PremiumRate = 0.10m,
        //        MinSalaryBase = 4001m,
        //        MaxSalaryBase = 5000m,
        //        EmployeeShare = 250m,
        //        EmployerShare = 500m,
        //        TotalContribution = 750m
        //    },
        //     new PHICTable
        //     {
        //         Id = Guid.Parse("11111111-1111-1111-1111-111111111115"),
        //         TenantId = Guid.Parse("C1B8AAAF-6BFF-4F68-97C7-626F16EA9197"),
        //         EffectiveDate = new DateOnly(2025, 1, 1),
        //         PremiumRate = 0.05m,          // 5% rate
        //         MinSalaryBase = 10000m,       // income floor
        //         MaxSalaryBase = 10000m,       // fixed floor bracket
        //         EmployeeShare = 250m,         // 5% of 10,000 = 500 / 2
        //         EmployerShare = 250m,
        //         TotalContribution = 500m
        //     },
        //    new PHICTable
        //    {
        //        Id = Guid.Parse("11111111-1111-1111-1111-111111111116"),
        //        TenantId = Guid.Parse("C1B8AAAF-6BFF-4F68-97C7-626F16EA9197"),
        //        EffectiveDate = new DateOnly(2025, 1, 1),
        //        PremiumRate = 0.05m,          // 5% rate
        //        MinSalaryBase = 100000m,      // income ceiling
        //        MaxSalaryBase = 999_999_999.99M,     // fixed ceiling bracket
        //        EmployeeShare = 2500m,        // 5% of 100,000 = 5,000 / 2
        //        EmployerShare = 2500m,
        //        TotalContribution = 5000m
        //    });
    }
}
internal class HDMFTableConfig : IEntityTypeConfiguration<HDMFTable>
{
    public void Configure(EntityTypeBuilder<HDMFTable> builder)
    {
        //builder.HasData(
        //    // Bracket 1: Salary ≤ 1,500
        //    new HDMFTable
        //    {
        //        Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        //        TenantId = Guid.Parse("C1B8AAAF-6BFF-4F68-97C7-626F16EA9197"),
        //        EffectiveDate = new DateOnly(2025, 1, 1),
        //        EmployerRate = 0.02m,              // 2%
        //        EmployeeRate = 0.01m,              // 1%
        //        MinSalaryBase = 1000m,
        //        MaxSalaryBase = 1500m,
        //        EmployeeShare = 15m,               // 1% of 1,500
        //        EmployerShare = 30m,               // 2% of 1,500
        //        TotalContribution = 45m
        //    },

        //    // Bracket 2: Salary > 1,500 up to 5,000
        //    new HDMFTable
        //    {
        //        Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
        //        TenantId = Guid.Parse("C1B8AAAF-6BFF-4F68-97C7-626F16EA9197"),
        //        EffectiveDate = new DateOnly(2025, 1, 1),
        //        EmployerRate = 0.02m,              // 2%
        //        EmployeeRate = 0.02m,              // 2%
        //        MinSalaryBase = 1501m,
        //        MaxSalaryBase = 999_999_999.99M,
        //        EmployeeShare = 100m,              // 2% of 5,000
        //        EmployerShare = 100m,              // 2% of 5,000
        //        TotalContribution = 200m
        //    });
    }
}
