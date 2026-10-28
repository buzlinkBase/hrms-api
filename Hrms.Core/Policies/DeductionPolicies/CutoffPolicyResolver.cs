namespace Hrms.Core.Policies.DeductionPolicies;

public class CutoffPolicyResolver : ICutoffPolicyResolver
{
    // A period's place in its month is decided by the cutoff it ENDS on: the first configured
    // cutoff day on or after ToDate's day (end of month resolved in ToDate's month). For the
    // usual 1-15 / 16-EOM periods that is the same answer the start day gives; for a group
    // whose cutoffs don't reach month-end (10/25) it is the only one that works -- Feb 26-Mar 10
    // ends on the 10th, March's first cutoff, although it starts after it.
    public bool IsFirstCutoff(DeductionPayloadContext context)
    {
        var frequency = context.Employee.PayrollFrequency;
        if (frequency == PayrollFrequency.MONTHLY || frequency == PayrollFrequency.DAILY)
            return true;

        // No usable cutoff configuration — throw the same exception GetCurrentCutoff does,
        // which callers (StatutoryScheduleResolver, the Table*SemiMonthlyCalculator classes)
        // catch and fail open on, instead of crashing payroll processing.
        var cutoffDays = ResolveEndingCutoffDays(context);
        if (cutoffDays.Count == 0)
            throw new CutoffMismatchException($"No cutoff days defined for {context.Employee.PayrollGroup?.Code}");

        return EndingSlot(context, cutoffDays) == 0;
    }

    public bool IsSecondCutoff(DeductionPayloadContext context)
    {
        var frequency = context.Employee.PayrollFrequency;
        if (frequency == PayrollFrequency.MONTHLY ||
            frequency == PayrollFrequency.DAILY) return false;

        var cutoffDays = ResolveEndingCutoffDays(context);
        if (cutoffDays.Count < 2) return false;
        var slot = EndingSlot(context, cutoffDays);

        // Weekly: the 2nd configured week. Semi-Monthly: any cutoff after the first.
        return frequency == PayrollFrequency.WEEKLY ? slot == 1 : slot >= 1;
    }

    public bool IsLastCutoff(DeductionPayloadContext context)
    {
        // A period crossing into a new month in a group whose cutoffs end at month-end (15/EOM)
        // is forced last, so the month it is credited to has its balance cleared before the next
        // one starts. A group whose periods roll over by design (10/25) doesn't need that: its
        // cross-month period is simply the first cutoff of the month it ends in.
        if (new IsCrossMonth().IsSatisfiedBy(context.Payload) && !StatutoryMonthResolver.RollsOverMonth(context.Employee.PayrollGroup))
            return true;

        var frequency = context.Employee.PayrollFrequency;
        if (frequency == PayrollFrequency.MONTHLY || frequency == PayrollFrequency.DAILY)
            return true;

        var cutoffDays = ResolveEndingCutoffDays(context);
        if (cutoffDays.Count == 0) return false;

        return EndingSlot(context, cutoffDays) == cutoffDays.Count - 1;
    }

    // Index of the cutoff the period ends on; past the last configured day counts as the last.
    private static int EndingSlot(DeductionPayloadContext context, List<int> cutoffDays)
    {
        var endDay = context.Payload.ToDate.Day;
        var slot = cutoffDays.FindIndex(day => endDay <= day);
        return slot < 0 ? cutoffDays.Count - 1 : slot;
    }

    // The configured cutoff days with end of month resolved in the month the period ends in.
    private static List<int> ResolveEndingCutoffDays(DeductionPayloadContext context)
    {
        var end = context.Payload.ToDate;
        return (context.Employee.PayrollGroup?.CutoffDays ?? [])
            .Select(cd => cd.IsEndOfMonth ? DateTime.DaysInMonth(end.Year, end.Month) : cd.Day)
            .OrderBy(d => d)
            .ToList();
    }


    public CutoffModel GetCurrentCutoff(DeductionPayloadContext context)
    {
        if (context.Employee.PayrollGroup == null)
            throw new CutoffMismatchException("Employee has no Payroll Group assigned.");

        var frequency = context.Employee.PayrollFrequency;
        var referenceDate = GetReferenceDate(context);
        var currentDay = referenceDate.Day;

        var cutoffModels = ResolveCutoffModelsAsModels(context);
        if (!cutoffModels.Any())
            throw new CutoffMismatchException($"No cutoff days defined for {context.Employee.PayrollGroup.Code}");

        // If Monthly, return the only cutoff available (usually end of month)
        if (frequency == PayrollFrequency.MONTHLY) return cutoffModels.First();

        // Find the first configured cutoff on or after the reference day — the bracket the
        // reference day currently falls into. Works for any number of cutoffs (the usual
        // two for Semi-Monthly, or however many a Weekly/custom schedule defines), rather
        // than assuming Semi-Monthly is always exactly two rows — payroll groups are no
        // longer locked to a fixed cutoff count.
        var match = cutoffModels.FirstOrDefault(cd => currentDay <= cd.Day);
        return match ?? cutoffModels.Last();
    }

    public CutoffModel GetFirstCutoff(DeductionPayloadContext context)
    {
        var cutoffModels = ResolveCutoffModelsAsModels(context);
        if (!cutoffModels.Any())
            throw new CutoffMismatchException("No cutoff days defined.");

        return cutoffModels.First();
    }

    public CutoffModel GetSecondCutoff(DeductionPayloadContext context)
    {
        var frequency = context.Employee.PayrollFrequency;
        if (frequency == PayrollFrequency.MONTHLY)
            throw new CutoffMismatchException("Monthly payroll frequency does not support a second cutoff.");

        var cutoffModels = ResolveCutoffModelsAsModels(context);
        if (cutoffModels.Count < 2)
            throw new CutoffMismatchException("No second cutoff defined in configuration.");

        return cutoffModels.Skip(1).First();
    }

    private DateOnly GetReferenceDate(DeductionPayloadContext context)
    {
        // Cross-month logic: if ToDate day is less than FromDate day, we've moved to next month
        return context.Payload.ToDate.Day < context.Payload.FromDate.Day
            ? context.Payload.ToDate
            : context.Payload.FromDate;
    }

    private List<int> ResolveCutoffModels(DeductionPayloadContext context)
    {

        var referenceDate = GetReferenceDate(context);
        // CutoffDays is a nullable navigation — payroll groups saved before cutoff-day
        // configuration was validated could still have none.
        return (context.Employee.PayrollGroup.CutoffDays ?? [])
            .Select(cd => cd.IsEndOfMonth
                ? DateTime.DaysInMonth(referenceDate.Year, referenceDate.Month)
                : cd.Day)
            .OrderBy(d => d)
            .ToList();
    }

    private List<CutoffModel> ResolveCutoffModelsAsModels(DeductionPayloadContext context)
    {
        var referenceDate = GetReferenceDate(context);
        return (context.Employee.PayrollGroup.CutoffDays ?? [])
            .Select(cd => new CutoffModel
            {
                Day = cd.IsEndOfMonth ? DateTime.DaysInMonth(referenceDate.Year, referenceDate.Month) : cd.Day,
                IsEndOfMonth = cd.IsEndOfMonth,
                Label = cd.Label
            })
            .OrderBy(cd => cd.Day)
            .ToList();
    }
}
public interface ICutoffPolicyResolver
{
    bool IsFirstCutoff(DeductionPayloadContext context);
    bool IsSecondCutoff(DeductionPayloadContext context);
    CutoffModel GetCurrentCutoff(DeductionPayloadContext context);
    CutoffModel GetFirstCutoff(DeductionPayloadContext context);
    CutoffModel GetSecondCutoff(DeductionPayloadContext context);
    bool IsLastCutoff(DeductionPayloadContext context);
}