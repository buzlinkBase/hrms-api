namespace Hrms.Core.Policies.DeductionPolicies;
public class CutoffPolicyResolver : ICutoffPolicyResolver
{
    public bool IsFirstCutoff(DeductionPayloadContext context)
    {
        var frequency = context.Employee.PayrollFrequency;
        var cutoffDays = ResolveCutoffModels(context);
        var currentDay = context.Payload.FromDate.Day;

        return frequency switch
        {
            PayrollFrequency.MONTHLY => true,
            PayrollFrequency.DAILY => true,
            PayrollFrequency.SEMI_MONTHLY => currentDay <= cutoffDays.First(),
            // For Weekly, we define "First Cutoff" as the first payroll occurrence of the month
            PayrollFrequency.WEEKLY => currentDay <= cutoffDays.First(),
            _ => currentDay <= cutoffDays.First()
        };
    }

    public bool IsSecondCutoff(DeductionPayloadContext context)
    {
        var frequency = context.Employee.PayrollFrequency;
        if (frequency == PayrollFrequency.MONTHLY ||
            frequency == PayrollFrequency.DAILY) return false;

        var cutoffDays = ResolveCutoffModels(context);
        var currentDay = context.Payload.FromDate.Day;

        if (frequency == PayrollFrequency.WEEKLY)
        {
            // For Weekly, the "Second Cutoff" is usually the 2nd week.
            // If there are 4-5 weeks, you may need a more complex 'WeekIndex' check.
            return cutoffDays.Count > 1 && currentDay > cutoffDays.First() && currentDay <= cutoffDays.Skip(1).First();
        }
        // Semi-Monthly logic
        return cutoffDays.Count > 1 && currentDay > cutoffDays.First();
    }

    public bool IsLastCutoff(DeductionPayloadContext context)
    {
        // 1. Force true if the period crosses into a new month. 
        // This ensures old month balances are cleared before the new month starts.
        if (new IsCrossMonth().IsSatisfiedBy(context.Payload))
            return true;

        var frequency = context.Employee.PayrollFrequency;

        if (frequency == PayrollFrequency.MONTHLY || frequency == PayrollFrequency.DAILY)
            return true;

        var cutoffDays = ResolveCutoffModels(context);
        if (!cutoffDays.Any()) return false;

        var referenceDate = GetReferenceDate(context);
        var currentDay = referenceDate.Day;
        var lastCutoffDay = cutoffDays.Last();

        if (frequency == PayrollFrequency.SEMI_MONTHLY)
        {
            return currentDay > cutoffDays.First();
        }

        if (frequency == PayrollFrequency.WEEKLY)
        {
            // Check if we are in the final slot of the configured weekly dates
            return currentDay >= lastCutoffDay ||
                   (cutoffDays.Count > 1 && currentDay > cutoffDays[cutoffDays.Count - 2]);
        }

        return currentDay >= lastCutoffDay;
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
        if (frequency == PayrollFrequency.SEMI_MONTHLY)
        {
            // Specifically look for the two brackets
            var firstCutoff = cutoffModels.First();

            // If we are even one day past the first cutoff, we belong to the second
            return currentDay <= firstCutoff.Day ? firstCutoff : cutoffModels.Last();
        }

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
        return context.Employee.PayrollGroup.CutoffDays
            .Select(cd => cd.IsEndOfMonth
                ? DateTime.DaysInMonth(referenceDate.Year, referenceDate.Month)
                : cd.Day)
            .OrderBy(d => d)
            .ToList();
    }

    private List<CutoffModel> ResolveCutoffModelsAsModels(DeductionPayloadContext context)
    {
        var referenceDate = GetReferenceDate(context);
        return context.Employee.PayrollGroup.CutoffDays
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