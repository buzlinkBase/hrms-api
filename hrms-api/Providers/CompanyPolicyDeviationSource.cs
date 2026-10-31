using DTR.Core;
using Hrms.Core.Services.Compliance;

namespace Hrms.Api.Providers;

// Company Settings > Attendance & Payroll Policy, read the way the policy screen reads it
// (GeneralSettingsController.GetCompanyPolicy) and checked the way its save checks it. Lives here
// rather than in Hrms.Core because the policy model is DTR.Core's.
public sealed class CompanyPolicyDeviationSource(GeneralSettingService settings, CompanyPolicyService policyService) : IDeviationSource
{
    public async Task<IEnumerable<DeviationFinding>> FindAsync(CancellationToken token)
    {
        var saved = await settings.GetSettingsAsync("Company");
        var policy = policyService.Transform(saved);
        var carrier = saved.TryGetValue(SettingKey.DeductionCarrierMode.ToString(), out var mode)
            ? GeneralSettingsUtil.ParseEnum(mode.Value, DeductionCarrierMode.EveryRun)
            : DeductionCarrierMode.EveryRun;
        var subject = new CompanyPolicySubject(
            policy.OtNdCalculationMethod,
            policy.HourlyRateDivisor,
            policy.OTEligibility,
            policy.IsHalfDayLateOn,
            policy.IsWholeDayLateOn,
            policy.NightDiffThreshold,
            policy.RequiredTakehomePercentage,
            carrier);
        return DeviationFindings.At(ComplianceAcknowledgmentService.CompanyScope, "Attendance & Payroll Policy",
            StatutoryDeviationRules.Evaluate(subject));
    }
}
