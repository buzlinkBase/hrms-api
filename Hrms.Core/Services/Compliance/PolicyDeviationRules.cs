using System.Globalization;
using Hrms.Domain;

namespace Hrms.Core.Services.Compliance;

// Company Policy (and client OT) settings that pay less than, or withhold more than, the Labor
// Code allows. Each rule is one Strategy registered in StatutoryDeviationRules.

public sealed class ShiftHoursDivisorRule : IStatutoryDeviationRule<CompanyPolicySubject>
{
    public IEnumerable<StatutoryDeviation> Evaluate(CompanyPolicySubject subject)
    {
        if (subject.HourlyRateDivisor != HourlyRateDivisor.ShiftMaxWorkingHours) yield break;
        yield return new StatutoryDeviation(
            "policy.hourly-divisor",
            "Hourly rate divisor",
            nameof(HourlyRateDivisor.Fixed8Hours),
            nameof(HourlyRateDivisor.ShiftMaxWorkingHours),
            "Dividing the daily rate by a shift longer than 8 hours lowers the hourly rate every hour bucket is paid from (Labor Code Art. 83).");
    }
}

public sealed class OvertimeOffsetRule : IStatutoryDeviationRule<OvertimeEligibilitySubject>
{
    public IEnumerable<StatutoryDeviation> Evaluate(OvertimeEligibilitySubject subject)
    {
        if (subject.Rule != OvertimeEligibilityRule.OffsetAgainstUndertimeOrLateness) yield break;
        yield return new StatutoryDeviation(
            "policy.ot-eligibility",
            "OT eligibility",
            "OT paid separately from late and undertime",
            nameof(OvertimeEligibilityRule.OffsetAgainstUndertimeOrLateness),
            "Undertime on one day may not be offset by overtime (Labor Code Art. 88).");
    }
}

public sealed class LatePenaltyRule : IStatutoryDeviationRule<CompanyPolicySubject>
{
    public IEnumerable<StatutoryDeviation> Evaluate(CompanyPolicySubject subject)
    {
        if (subject.IsHalfDayLateOn)
            yield return new StatutoryDeviation(
                "policy.late-half-day",
                "Half-day late penalty",
                "Deduct only the minutes late",
                "On",
                "Deducting half a day for lateness takes more than the time actually lost.");
        if (subject.IsWholeDayLateOn)
            yield return new StatutoryDeviation(
                "policy.late-whole-day",
                "Whole-day late penalty",
                "Deduct only the minutes late",
                "On",
                "Deducting a whole day for lateness takes more than the time actually lost.");
    }
}

public sealed class NightDiffThresholdRule : IStatutoryDeviationRule<CompanyPolicySubject>
{
    public IEnumerable<StatutoryDeviation> Evaluate(CompanyPolicySubject subject)
    {
        if (subject.NightDiffThreshold <= 0) yield break;
        yield return new StatutoryDeviation(
            "policy.nd-threshold",
            "Night differential threshold",
            "0 minutes",
            $"{subject.NightDiffThreshold.ToString("0.##", CultureInfo.InvariantCulture)} minutes",
            "Every hour worked from 10 PM to 6 AM earns the night differential (Labor Code Art. 86).");
    }
}

public sealed class MinimumTakeHomeRule : IStatutoryDeviationRule<CompanyPolicySubject>
{
    public IEnumerable<StatutoryDeviation> Evaluate(CompanyPolicySubject subject)
    {
        if (subject.RequiredTakehomePercentage <= 0) yield break;
        yield return new StatutoryDeviation(
            "policy.min-takehome",
            "Minimum take-home pay",
            "No floor that skips statutory deductions",
            $"{subject.RequiredTakehomePercentage.ToString("0.##", CultureInfo.InvariantCulture)}%",
            "When the floor is reached, PhilHealth, Pag-IBIG, withholding tax and loans are not deducted for that run.");
    }
}

public sealed class ManualDeductionCarrierRule : IStatutoryDeviationRule<CompanyPolicySubject>
{
    public IEnumerable<StatutoryDeviation> Evaluate(CompanyPolicySubject subject)
    {
        if (subject.DeductionCarrierMode != DeductionCarrierMode.Manual) yield break;
        yield return new StatutoryDeviation(
            "policy.deduction-carrier",
            "Deduction carrier",
            nameof(DeductionCarrierMode.EveryRun),
            nameof(DeductionCarrierMode.Manual),
            "A cutoff where no run is picked to carry them leaves the month's contributions undeducted.");
    }
}

public sealed class RetirementAccrualRule : IStatutoryDeviationRule<ClientSubject>
{
    // RA 7641: half a month's salary per year of service, which works out to 22.5 days.
    public const decimal StatutoryDaysPerYear = 22.5m;

    public IEnumerable<StatutoryDeviation> Evaluate(ClientSubject subject)
    {
        // Not given (null/0) is no accrual at all, not a lower one -- the company pays retirement pay itself.
        if (subject.RetirementDaysPerYear is not > 0 || subject.RetirementDaysPerYear >= StatutoryDaysPerYear) yield break;
        yield return new StatutoryDeviation(
            "client.retirement",
            "Retirement accrual (days/year)",
            StatutoryDaysPerYear.ToString("0.00", CultureInfo.InvariantCulture),
            subject.RetirementDaysPerYear.Value.ToString("0.00", CultureInfo.InvariantCulture),
            "Accrues less than the retirement pay RA 7641 requires for each year of service.");
    }
}
