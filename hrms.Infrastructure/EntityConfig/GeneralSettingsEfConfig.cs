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
            new GeneralSetting { Description = SettingKey.OTEligibility.ToString(), Value = OvertimeEligibilityRule.IndependentOfAttendanceIssues.ToString() },
            new GeneralSetting { Description = SettingKey.OTInclusion.ToString(), Value = OvertimeInclusionPolicy.UsePostShiftWork.ToString() },
            new GeneralSetting { Description = SettingKey.IsHalfDayLateOn.ToString(), Value = false.ToString() },
            new GeneralSetting { Description = SettingKey.HalfDayLateThresholdMinutes.ToString(), Value = "0" },
            new GeneralSetting { Description = SettingKey.IsWholeDayLateOn.ToString(), Value = false.ToString() },
            new GeneralSetting { Description = SettingKey.WholeDayLateThresholdMinutes.ToString(), Value = "0" },
            new GeneralSetting { Description = SettingKey.NightDiffThreshold.ToString(), Value = "0" },
            new GeneralSetting { Description = SettingKey.AttFillLimit.ToString(), Value = ManualEntryLimitEnum.NOLIMIT.ToString() },
            new GeneralSetting { Description = SettingKey.HolidayTimeBasis.ToString(), Value = HolidayTimeBasis.BasedOnTimeInDayType.ToString() },
            new GeneralSetting { Description = SettingKey.IsHolPlusReg.ToString(), Value = true.ToString() },
            new GeneralSetting { Description = SettingKey.HolidayColumnPresentation.ToString(), Value = HolidayCreditMode.AutoCredit.ToString() }
        );
    }
}