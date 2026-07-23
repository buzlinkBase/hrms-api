using Hrms.Domain;
using Hrms.Domain.Entities.EmployeeEntities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hrms.Infrastructure.EntityConfig;

public class EmployeeConfig : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {

        // builder.HasIndex(x => x.BioId)
        //.IsUnique()
        //.HasFilter("(CASE WHEN BioId = 0 THEN NULL ELSE BioId END)");

        builder.HasIndex(x => x.FirstName);
        builder.HasIndex(x => x.MiddleName);
        builder.HasIndex(x => x.LastName);
        builder.HasIndex(x => x.Suffix);

        builder.Property(x => x.SalaryType)
          .HasConversion(
                v => v.ToString(),
                v => EnumParserConfig.SafeParseEnum(v, SalaryType.MONTHLY_VARIABLE)
            );

        builder.Property(x => x.JobLevel)
          .HasConversion(
                v => v.ToString(),
                v => (JobLevelOption)Enum.Parse(typeof(JobLevelOption), v)
            );
    }
}
public class SSSRateConfig : IEntityTypeConfiguration<SSSRate>
{
    public void Configure(EntityTypeBuilder<SSSRate> builder)
    {
        builder.Property(x => x.ComputationType)
          .HasConversion(
                v => v.ToString(),
                v => EnumParserConfig.SafeParseEnum(v, ComputationBasis.None)
            );

        //builder.Property(x => x.Frequency)
        //  .HasConversion(
        //        v => v.ToString(),
        //        v => EnumParserConfig.SafeParseEnum(v, StatutoryDeductionSchedule.PerPayroll)
        //    );

        builder.HasOne(x => x.Employee)
        .WithOne(x => x.SSSRate)
        .HasForeignKey<SSSRate>(x => x.EmployeeId);

        //builder.HasData(
        //new SSSRate
        //{
        //    Id = Guid.NewGuid(),
        //    TenantId = Guid.Parse("C1B8AAAF-6BFF-4F68-97C7-626F16EA9197"),
        //    EmployeeId = Guid.Parse("398BBB60-4A8F-4095-A478-59B4F4E6A22F"),
        //    ComputationType = ComputationBasis.Table
        //}
    }
}
public class PHICRateConfig : IEntityTypeConfiguration<PHICRate>
{
    public void Configure(EntityTypeBuilder<PHICRate> builder)
    {
        builder.Property(x => x.ComputationType)
          .HasConversion(
                v => v.ToString(),
                v => EnumParserConfig.SafeParseEnum(v, ComputationBasis.None)
            );

        //builder.Property(x => x.Frequency)
        //  .HasConversion(
        //        v => v.ToString(),
        //        v => EnumParserConfig.SafeParseEnum(v, StatutoryDeductionSchedule.PerPayroll)
        //    );

        builder.HasOne(x => x.Employee)
        .WithOne(x => x.PHICRate)
        .HasForeignKey<PHICRate>(x => x.EmployeeId);


        //builder.HasData(
        //    new PHICRate
        //    {
        //        Id = Guid.NewGuid(),
        //        TenantId = Guid.Parse("C1B8AAAF-6BFF-4F68-97C7-626F16EA9197"),
        //        EmployeeId = Guid.Parse("398BBB60-4A8F-4095-A478-59B4F4E6A22F"),
        //        ComputationType = ComputationBasis.Table
        //    }


    }
}
public class HDMFRateConfig : IEntityTypeConfiguration<HDMFRate>
{
    public void Configure(EntityTypeBuilder<HDMFRate> builder)
    {
        builder.Property(x => x.ComputationType)
          .HasConversion(
                v => v.ToString(),
                v => EnumParserConfig.SafeParseEnum(v, ComputationBasis.None)
            );

        //   builder.Property(x => x.Frequency)
        //.HasConversion(
        //      v => v.ToString(),
        //      v => EnumParserConfig.SafeParseEnum(v, StatutoryDeductionSchedule.PerPayroll)
        //  );

        builder.HasOne(x => x.Employee)
        .WithOne(x => x.HDMFRate)
        .HasForeignKey<HDMFRate>(x => x.EmployeeId);


        //builder.HasData(
        //new HDMFRate
        //{
        //    Id = Guid.NewGuid(),
        //    TenantId = Guid.Parse("C1B8AAAF-6BFF-4F68-97C7-626F16EA9197"),
        //    EmployeeId = Guid.Parse("398BBB60-4A8F-4095-A478-59B4F4E6A22F"),
        //    ComputationType = ComputationBasis.Table
        //}
    }
}
public class TaxRateConfig : IEntityTypeConfiguration<TaxRate>
{
    public void Configure(EntityTypeBuilder<TaxRate> builder)
    {
        builder.Property(x => x.ComputationType)
          .HasConversion(
                v => v.ToString(),
                v => EnumParserConfig.SafeParseEnum(v, ComputationBasis.None)
            );

        //   builder.Property(x => x.Frequency)
        //.HasConversion(
        //      v => v.ToString(),
        //      v => EnumParserConfig.SafeParseEnum(v, StatutoryDeductionSchedule.PerPayroll)
        //  );


        builder.HasOne(x => x.Employee)
        .WithOne(x => x.TaxRate)
        .HasForeignKey<TaxRate>(x => x.EmployeeId);
    }
}
public class EmployeeSettingConfig : IEntityTypeConfiguration<EmployeeSetting>
{
    public void Configure(EntityTypeBuilder<EmployeeSetting> builder)
    {
        builder.HasOne(x => x.Employee)
        .WithOne(x => x.Settings)
        .HasForeignKey<EmployeeSetting>(x => x.EmployeeId);

        //builder.HasData(
        //    new EmployeeSetting
        //    {
        //        Id = Guid.NewGuid(),
        //        TenantId = Guid.Parse("C1B8AAAF-6BFF-4F68-97C7-626F16EA9197"),
        //        EmployeeId = Guid.Parse("398BBB60-4A8F-4095-A478-59B4F4E6A22F"),
        //        IsEligibleForOvertime = true,
        //        IsEligibleFor13thMonth = true,
        //        IsEligibleForHolidayPay = true,
        //        IsEligibleForLeaveCredits = true,
        //        IsEligibleForNightDifferential = true
        //    });
    }
}
