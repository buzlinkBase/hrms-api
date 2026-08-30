namespace Hrms.Core.Policies.DeductionPolicies;

// Chain of Responsibility (GoF): each rule either finalizes the cutoff-split divisor for a
// Semi-Monthly payroll line or defers to the next rule by returning null. Order matters — a
// schedule-driven full release always wins, even over a cross-month or mid-period-hire
// adjustment, matching the original inline logic this was extracted from.
public interface ICutoffDivisorRule
{
    int? Resolve(DeductionPayloadContext context, ICutoffPolicyResolver resolver, StatutoryReleaseAction scheduleAction, int configuredCutoffs);
}

// FirstHalfMonth/SecondHalfMonth on their matching cutoff release the full remaining
// balance now instead of splitting it — takes priority over every other rule.
public class FullBalanceNowDivisorRule : ICutoffDivisorRule
{
    public int? Resolve(DeductionPayloadContext context, ICutoffPolicyResolver resolver, StatutoryReleaseAction scheduleAction, int configuredCutoffs) =>
        scheduleAction == StatutoryReleaseAction.ReleaseFullBalanceNow ? 1 : null;
}

// A payroll period that crosses into a new month deducts all remaining balance immediately
// rather than splitting it further.
public class CrossMonthDivisorRule : ICutoffDivisorRule
{
    public int? Resolve(DeductionPayloadContext context, ICutoffPolicyResolver resolver, StatutoryReleaseAction scheduleAction, int configuredCutoffs) =>
        new IsCrossMonth().IsSatisfiedBy(context.Payload) ? 1 : null;
}

// Employee hired mid-month, after the period's first cutoff. Currently resolves to the same
// divisor the default fallback would already give — kept as its own rule (rather than
// folded away) so a future change to how late hires are prorated only touches this class.
public class MidPeriodHireDivisorRule : ICutoffDivisorRule
{
    public int? Resolve(DeductionPayloadContext context, ICutoffPolicyResolver resolver, StatutoryReleaseAction scheduleAction, int configuredCutoffs)
    {
        var hiredThisMonth = context.Employee.HireDate.Year == context.Payload.FromDate.Year &&
                              context.Employee.HireDate.Month == context.Payload.FromDate.Month;
        if (!hiredThisMonth) return null;

        var currentCutoff = resolver.GetCurrentCutoff(context);
        return context.Employee.HireDate.Day > currentCutoff.Day ? configuredCutoffs : null;
    }
}

// Drives the chain and supplies the default fallback (the payroll group's actual configured
// cutoff count) when no rule finalizes a divisor.
public class CutoffDivisorResolver
{
    private static readonly List<ICutoffDivisorRule> Rules = new()
    {
        new FullBalanceNowDivisorRule(),
        new CrossMonthDivisorRule(),
        new MidPeriodHireDivisorRule(),
    };

    public int Resolve(DeductionPayloadContext context, ICutoffPolicyResolver resolver, StatutoryReleaseAction scheduleAction)
    {
        // Semi-Monthly payroll groups are validated to have at least two configured
        // cutoffs (PayrollGroupService.CreateValidatorAsync), but the count is no longer
        // assumed to always be exactly two now that cutoffs are admin-configurable.
        var configuredCutoffsRaw = context.Employee.PayrollGroup?.CutoffDays?.Count ?? 2;
        var configuredCutoffs = configuredCutoffsRaw > 0 ? configuredCutoffsRaw : 2;

        foreach (var rule in Rules)
        {
            var result = rule.Resolve(context, resolver, scheduleAction, configuredCutoffs);
            if (result.HasValue) return result.Value;
        }

        return configuredCutoffs;
    }
}
