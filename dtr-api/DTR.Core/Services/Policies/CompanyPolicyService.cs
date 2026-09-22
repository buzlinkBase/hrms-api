namespace DTR.Core;

public class CompanyPolicyRule
{
    public OvertimeInclusionPolicy OTInclusionPolicy { get; set; }
    public OvertimeEligibilityRule OTEligibility { get; set; }
    public bool IsHalfDayLateOn { get; set; }
    public bool IsWholeDayLateOn { get; set; }
    public double HalfDayLateThresholdMinutes { get; set; }
    public double WholeDayLateThresholdMinutes { get; set; }
    public double NightDiffThreshold { get; set; }
    public ManualEntryLimitEnum AttFillLimit { get; set; }
    public HolidayTimeBasis HolidayTimeBasis { get; set; }
    public bool IsHolPlusReg { get; set; }

    public double TimeInAllowance { get; set; } = -120;
    public double DoublePunchGap { get; set; } = 5;
    public bool CheckAfterHoliday { get; set; } = false;
    public bool WaivePriorDayRequirement { get; set; } = false;
    public CrossMonthStatutoryCreditPolicy CrossMonthStatutoryCreditPolicy { get; set; } = CrossMonthStatutoryCreditPolicy.CutoffStartMonth;
    public CrossMonthStatutoryCreditPolicy WTaxCrossMonthCreditPolicy { get; set; } = CrossMonthStatutoryCreditPolicy.CutoffEndMonth;

    // Minimum take-home floor, as a percentage of gross income — see DeductionValidator.CanApply.
    public double RequiredTakehomePercentage { get; set; } = 10;

    // Mirrored here purely so GeneralSettingsController's GET can read it the same way as every
    // other company policy field — never actually consumed by DTR-side logic. The real consumer
    // is Hrms.Core.Pipelines.PayrollContext's own CompanyPolicyRule (hrms.Domain/ValueObjects/
    // PaginationPayload.cs), populated separately by PayrollRangeContextComposerService. See
    // NightDiffOTCategoryPolicies.SingleCategoryNDOTPolicy.
    public OtNdCalculationMethod OtNdCalculationMethod { get; set; } = OtNdCalculationMethod.Compounded;

}

public class CompanyPolicyService
{
    public CompanyPolicyRule Transform(Dictionary<string, GeneralSettingModel> data)
    {
        CompanyPolicyRule policy = new CompanyPolicyRule();
        SetOTeligibilityRule(policy, data);
        SetOTInclusionRule(policy, data);
        SetHalfDayLateRule(policy, data);
        SetWholeDayLateRule(policy, data);
        SetNightDiffThreshold(policy, data);
        AutoFillLimit(policy, data);
        SetHolidayComputationBasis(policy, data);
        SetIsHolidayAddedInHolidayColumn(policy, data);
        SetTimeInAllowance(policy, data);
        SetDoublePunchGap(policy, data);
        SetCheckAfterHoliday(policy, data);
        SetWaivePriorDayRequirement(policy, data);
        SetCrossMonthStatutoryCreditPolicy(policy, data);
        SetWTaxCrossMonthCreditPolicy(policy, data);
        SetRequiredTakehomePercentage(policy, data);
        SetOtNdCalculationMethod(policy, data);
        return policy;
    }

    private void SetOTeligibilityRule(CompanyPolicyRule policy, Dictionary<string, GeneralSettingModel> data)
    {
        policy.OTEligibility = OvertimeEligibilityRule.IndependentOfAttendanceIssues;
        if (data.TryGetValue(SettingKey.OTEligibility.ToString(), out GeneralSettingModel? val1))
        {
            var settingvalue = GeneralSettingsUtil.ParseEnum(val1.Value, OvertimeEligibilityRule.RequireFullRegularHours);
            policy.OTEligibility = settingvalue;
        }
    }
    private void SetOTInclusionRule(CompanyPolicyRule policy, Dictionary<string, GeneralSettingModel> data)
    {
        policy.OTInclusionPolicy = OvertimeInclusionPolicy.UsePostShiftWork;
        if (data.TryGetValue(SettingKey.OTInclusion.ToString(), out GeneralSettingModel? OTRule))
        {
            var settingvalue = GeneralSettingsUtil.ParseEnum(OTRule.Value, OvertimeInclusionPolicy.UsePostShiftWork);
            policy.OTInclusionPolicy = settingvalue;
        }
    }
    private void SetHalfDayLateRule(CompanyPolicyRule policy, Dictionary<string, GeneralSettingModel> data)
    {
        policy.IsHalfDayLateOn = false;
        policy.HalfDayLateThresholdMinutes = 0;
        if (data.TryGetValue(SettingKey.IsHalfDayLateOn.ToString(), out GeneralSettingModel? IsLateOn))
        {
            var settingvalue = GeneralSettingsUtil.ParseBool(IsLateOn.Value, false);
            policy.IsHalfDayLateOn = settingvalue;
        }

        if (data.TryGetValue(SettingKey.HalfDayLateThresholdMinutes.ToString(), out GeneralSettingModel? minutes))
        {
            var val = GeneralSettingsUtil.ParseDouble(minutes.Value, 0);
            policy.HalfDayLateThresholdMinutes = val;
        }
    }
    private void SetWholeDayLateRule(CompanyPolicyRule policy, Dictionary<string, GeneralSettingModel> data)
    {
        policy.IsWholeDayLateOn = false;
        policy.WholeDayLateThresholdMinutes = 0;
        if (data.TryGetValue(SettingKey.IsWholeDayLateOn.ToString(), out GeneralSettingModel? IsLateOn))
        {
            var settingvalue = GeneralSettingsUtil.ParseBool(IsLateOn.Value, false);
            policy.IsWholeDayLateOn = settingvalue;
        }

        if (data.TryGetValue(SettingKey.WholeDayLateThresholdMinutes.ToString(), out GeneralSettingModel? minutes))
        {
            var val = GeneralSettingsUtil.ParseDouble(minutes.Value, 0);
            policy.WholeDayLateThresholdMinutes = val;
        }
    }
    private void SetNightDiffThreshold(CompanyPolicyRule policy, Dictionary<string, GeneralSettingModel> data)
    {
        policy.NightDiffThreshold = 0;
        if (data.TryGetValue(SettingKey.NightDiffThreshold.ToString(), out GeneralSettingModel? threshold))
        {
            var settingvalue = GeneralSettingsUtil.ParseDouble(threshold.Value, 0);
            policy.NightDiffThreshold = settingvalue;
        }
    }
    private void AutoFillLimit(CompanyPolicyRule policy, Dictionary<string, GeneralSettingModel> data)
    {
        policy.AttFillLimit = ManualEntryLimitEnum.NOLIMIT;
        if (data.TryGetValue(SettingKey.AttFillLimit.ToString(), out GeneralSettingModel? limit))
        {
            var settingvalue = GeneralSettingsUtil.ParseEnum<ManualEntryLimitEnum>(limit.Value, 0);
            policy.AttFillLimit = settingvalue;
        }
    }

    private void SetHolidayComputationBasis(CompanyPolicyRule policy, Dictionary<string, GeneralSettingModel> data)
    {
        policy.HolidayTimeBasis = HolidayTimeBasis.BasedOnTimeInDayType;
        if (data.TryGetValue(SettingKey.HolidayTimeBasis.ToString(), out GeneralSettingModel? holbasis))
        {
            var settingvalue = GeneralSettingsUtil.ParseEnum(holbasis.Value, HolidayTimeBasis.BasedOnTimeInDayType);
            policy.HolidayTimeBasis = settingvalue;
        }
    }
    private void SetIsHolidayAddedInHolidayColumn(CompanyPolicyRule policy, Dictionary<string, GeneralSettingModel> data)
    {
        policy.IsHolPlusReg = false;
        if (data.TryGetValue(SettingKey.IsHolPlusReg.ToString(), out GeneralSettingModel? IsHolPlusReg))
        {
            var settingvalue = GeneralSettingsUtil.ParseBool(IsHolPlusReg.Value, false);
            policy.IsHolPlusReg = settingvalue;
        }
    }
    private void SetTimeInAllowance(CompanyPolicyRule policy, Dictionary<string, GeneralSettingModel> data)
    {
        policy.TimeInAllowance = -120;
        if (data.TryGetValue(SettingKey.TimeInAllowance.ToString(), out GeneralSettingModel? TimeInAllowance))
        {
            var settingvalue = GeneralSettingsUtil.ParseInt(TimeInAllowance.Value, -120);
            policy.TimeInAllowance = settingvalue;
        }
    }

    private void SetDoublePunchGap(CompanyPolicyRule policy, Dictionary<string, GeneralSettingModel> data)
    {
        policy.DoublePunchGap = 2;
        if (data.TryGetValue(SettingKey.DoublePunchGap.ToString(), out GeneralSettingModel? DoublePunchGap))
        {
            var settingvalue = GeneralSettingsUtil.ParseInt(DoublePunchGap.Value, 2);
            policy.DoublePunchGap = settingvalue;
        }
    }

    private void SetCheckAfterHoliday(CompanyPolicyRule policy, Dictionary<string, GeneralSettingModel> data)
    {
        policy.CheckAfterHoliday = false;
        if (data.TryGetValue(SettingKey.CheckAfterHoliday.ToString(), out GeneralSettingModel? CheckAfterHoliday))
        {
            var settingvalue = GeneralSettingsUtil.ParseBool(CheckAfterHoliday.Value, false);
            policy.CheckAfterHoliday = settingvalue;
        }
    }

    private void SetWaivePriorDayRequirement(CompanyPolicyRule policy, Dictionary<string, GeneralSettingModel> data)
    {
        policy.WaivePriorDayRequirement = false;
        if (data.TryGetValue(SettingKey.WaivePriorDayRequirement.ToString(), out GeneralSettingModel? WaivePriorDayRequirement))
        {
            var settingvalue = GeneralSettingsUtil.ParseBool(WaivePriorDayRequirement.Value, false);
            policy.WaivePriorDayRequirement = settingvalue;
        }
    }

    private void SetCrossMonthStatutoryCreditPolicy(CompanyPolicyRule policy, Dictionary<string, GeneralSettingModel> data)
    {
        policy.CrossMonthStatutoryCreditPolicy = CrossMonthStatutoryCreditPolicy.CutoffStartMonth;
        if (data.TryGetValue(SettingKey.CrossMonthStatutoryCreditPolicy.ToString(), out GeneralSettingModel? creditPolicy))
        {
            var settingvalue = GeneralSettingsUtil.ParseEnum(creditPolicy.Value, CrossMonthStatutoryCreditPolicy.CutoffStartMonth);
            policy.CrossMonthStatutoryCreditPolicy = settingvalue;
        }
    }

    private void SetWTaxCrossMonthCreditPolicy(CompanyPolicyRule policy, Dictionary<string, GeneralSettingModel> data)
    {
        policy.WTaxCrossMonthCreditPolicy = CrossMonthStatutoryCreditPolicy.CutoffEndMonth;
        if (data.TryGetValue(SettingKey.WTaxCrossMonthCreditPolicy.ToString(), out GeneralSettingModel? creditPolicy))
        {
            var settingvalue = GeneralSettingsUtil.ParseEnum(creditPolicy.Value, CrossMonthStatutoryCreditPolicy.CutoffEndMonth);
            policy.WTaxCrossMonthCreditPolicy = settingvalue;
        }
    }

    private void SetRequiredTakehomePercentage(CompanyPolicyRule policy, Dictionary<string, GeneralSettingModel> data)
    {
        policy.RequiredTakehomePercentage = 10;
        if (data.TryGetValue(SettingKey.RequiredTakehomePercentage.ToString(), out GeneralSettingModel? takehome))
        {
            var settingvalue = GeneralSettingsUtil.ParseDouble(takehome.Value, 10);
            policy.RequiredTakehomePercentage = settingvalue;
        }
    }

    private void SetOtNdCalculationMethod(CompanyPolicyRule policy, Dictionary<string, GeneralSettingModel> data)
    {
        policy.OtNdCalculationMethod = OtNdCalculationMethod.Compounded;
        if (data.TryGetValue(SettingKey.OtNdCalculationMethod.ToString(), out GeneralSettingModel? method))
        {
            var settingvalue = GeneralSettingsUtil.ParseEnum(method.Value, OtNdCalculationMethod.Compounded);
            policy.OtNdCalculationMethod = settingvalue;
        }
    }
}
