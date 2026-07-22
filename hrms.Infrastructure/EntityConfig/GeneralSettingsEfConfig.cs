using Hrms.Domain;
using Hrms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hrms.Infrastructure.EntityConfig;

internal class GeneralSettingsEfConfig : IEntityTypeConfiguration<GeneralSetting>
{
    public void Configure(EntityTypeBuilder<GeneralSetting> builder)
    {
        builder.HasData(
        new GeneralSetting { Id = Guid.Parse("A2618E39-1A02-4055-989A-7B3BCC61F7B3"), IdentityType = "Company", Description = SettingKey.OTEligibility.ToString(), Value = OvertimeEligibilityRule.IndependentOfAttendanceIssues.ToString(), Status = "Active", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
        new GeneralSetting { Id = Guid.Parse("B1A2C3D4-E5F6-4789-ABCD-1234567890AB"), IdentityType = "Company", Description = SettingKey.OTInclusion.ToString(), Value = OvertimeInclusionPolicy.UsePostShiftWork.ToString(), Status = "Active", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
        new GeneralSetting { Id = Guid.Parse("C2B3A4D5-F6E7-4890-BCDA-2345678901BC"), IdentityType = "Company", Description = SettingKey.IsHalfDayLateOn.ToString(), Value = false.ToString(), Status = "Active", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
        new GeneralSetting { Id = Guid.Parse("D3C4B5A6-E7F8-4901-CDAB-3456789012CD"), IdentityType = "Company", Description = SettingKey.HalfDayLateThresholdMinutes.ToString(), Value = "0", Status = "Active", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
        new GeneralSetting { Id = Guid.Parse("E4D5C6B7-F8E9-4012-DABC-4567890123DE"), IdentityType = "Company", Description = SettingKey.IsWholeDayLateOn.ToString(), Value = false.ToString(), Status = "Active", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
        new GeneralSetting { Id = Guid.Parse("F5E6D7C8-9012-4123-ABCD-5678901234EF"), IdentityType = "Company", Description = SettingKey.WholeDayLateThresholdMinutes.ToString(), Value = "0", Status = "Active", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
        new GeneralSetting { Id = Guid.Parse("0123F5E6-D7C8-4234-BCDA-6789012345FA"), IdentityType = "Company", Description = SettingKey.NightDiffThreshold.ToString(), Value = "0", Status = "Active", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
        new GeneralSetting { Id = Guid.Parse("1234A5B6-C7D8-4345-CDAB-7890123456AB"), IdentityType = "Company", Description = SettingKey.AttFillLimit.ToString(), Value = ManualEntryLimitEnum.NOLIMIT.ToString(), Status = "Active", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
        new GeneralSetting { Id = Guid.Parse("2345B6C7-D8E9-4456-DABC-8901234567BC"), IdentityType = "Company", Description = SettingKey.HolidayTimeBasis.ToString(), Value = HolidayTimeBasis.BasedOnTimeInDayType.ToString(), Status = "Active", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
        new GeneralSetting { Id = Guid.Parse("3456C7D8-E9F0-4567-ABCD-9012345678CD"), IdentityType = "Company", Description = SettingKey.IsHolPlusReg.ToString(), Value = true.ToString(), Status = "Active", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
        new GeneralSetting { Id = Guid.Parse("4567D8E9-F012-4678-BCDA-0123456789DE"), IdentityType = "Company", Description = SettingKey.HolidayColumnPresentation.ToString(), Value = HolidayCreditMode.AutoCredit.ToString(), Status = "Active", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
    );
    }
}