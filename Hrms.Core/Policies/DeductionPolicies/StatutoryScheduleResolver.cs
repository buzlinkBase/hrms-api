namespace Hrms.Core.Policies.DeductionPolicies;

public enum StatutoryReleaseAction
{
    /// <summary>PerPayroll, or Monthly/Daily where the setting is a structural no-op —
    /// run the calculator's existing divisor/proration logic unchanged.</summary>
    UseDefaultProration,
    /// <summary>FirstHalfMonth on the first cutoff of the month, or SecondHalfMonth on
    /// the last cutoff — withhold the full remaining balance right now.</summary>
    ReleaseFullBalanceNow,
    /// <summary>Gated out — withhold nothing on this cutoff.</summary>
    ReleaseNothing,
}

/// <summary>
/// Decides, once per calculator call, whether a Semi-Monthly/Weekly cutoff should use the
/// existing prorated split (PerPayroll), release the full remaining statutory balance right
/// now (FirstHalfMonth/SecondHalfMonth on their matching cutoff), or release nothing this
/// cutoff — based on PayrollGroup.StatutoryDeductionSchedule. Monthly/Daily frequencies only
/// ever have one release point, so the setting never changes their behavior.
/// </summary>
public static class StatutoryScheduleResolver
{
    public static StatutoryReleaseAction Resolve(DeductionPayloadContext context, ICutoffPolicyResolver resolver)
    {
        var frequency = context.Employee.PayrollFrequency;
        if (frequency == PayrollFrequency.MONTHLY || frequency == PayrollFrequency.DAILY)
            return StatutoryReleaseAction.UseDefaultProration;

        var schedule = context.Employee.PayrollGroup?.StatutoryDeductionSchedule
            ?? StatutoryDeductionSchedule.PerPayroll;
        if (schedule == StatutoryDeductionSchedule.PerPayroll)
            return StatutoryReleaseAction.UseDefaultProration;

        bool isFirst, isLast;
        try
        {
            isFirst = resolver.IsFirstCutoff(context);
            isLast = resolver.IsLastCutoff(context);
        }
        catch (CutoffMismatchException)
        {
            // No usable cutoff config — fail open to today's behavior rather than throw.
            return StatutoryReleaseAction.UseDefaultProration;
        }

        return schedule switch
        {
            StatutoryDeductionSchedule.FirstHalfMonth => isFirst
                ? StatutoryReleaseAction.ReleaseFullBalanceNow
                : StatutoryReleaseAction.ReleaseNothing,
            StatutoryDeductionSchedule.SecondHalfMonth => isLast
                ? StatutoryReleaseAction.ReleaseFullBalanceNow
                : StatutoryReleaseAction.ReleaseNothing,
            _ => StatutoryReleaseAction.UseDefaultProration,
        };
    }
}
