namespace DTR.Core;
/// <summary>
/// Rule specification: is the employee eligible for holiday pay for the given period?
/// Delegates to a type-specific evaluator (legal vs. special holiday rules differ).
/// </summary>
public class IsEligibleForHoliday : IRuleSpecification
{
    private readonly HolidayType _holidayType;
    public IsEligibleForHoliday(HolidayType holidayType)
    {
        _holidayType = holidayType;
    }
    public bool IsSatisfiedBy(TimeRange input, TimeContext context)
    {
        if (new IsGovFundedLeaved().IsSatisfiedBy(input, context))
        {
            //not eligible for holiday on leave during maternity and SL
            return false;
        }

        var hasSpecialLeave = context.Payload.Data.CurrentLeaves
           .Where(x => x.Leave.PaySource == PaySource.Government ||
           x.PayoutMode == PayoutMode.OneTime ||
           x.Leave.PaySource == PaySource.Shared).Any();

        //not eligble if has maternity/gov funded
        if (hasSpecialLeave) return false;

        var evaluator = HolidayEligibilityEvaluatorFactory.Create(_holidayType);
        return evaluator.Evaluate(input, context);

    }
}

public interface IHolidayEligibilityEvaluator
{
    bool Evaluate(TimeRange input, TimeContext context);
}

public static class HolidayEligibilityEvaluatorFactory
{
    public static IHolidayEligibilityEvaluator Create(HolidayType type) => type switch
    {
        HolidayType.SPECIAL => new SpecialHolidayEligibilityEvaluator(),
        HolidayType.LEGAL => new LegalHolidayEligibilityEvaluator(),
        _ => throw new NotImplementedException($"No {nameof(IHolidayEligibilityEvaluator)} registered for holiday type '{type}'.")
    };
}

/// <summary>
/// Special (non-legal) holidays grant automatic eligibility — there is no
/// attendance qualification requirement.
/// </summary>
public class SpecialHolidayEligibilityEvaluator : IHolidayEligibilityEvaluator
{
    public bool Evaluate(TimeRange input, TimeContext context) => true;
}

/// <summary>
/// Legal holiday eligibility follows DOLE's "qualifying day" rule:
///   1. If the employee actually worked the holiday itself, they're eligible outright.
///   2. Otherwise, eligibility depends on the *day before* the holiday: the employee
///      must have worked it, been on paid leave, or otherwise had a qualifying day.
///      Non-qualifying days (rest day, skipped, special-non-working with no pay) are
///      skipped over when searching backward for that qualifying day.
///      This prior-day requirement can be waived company-wide via
///      <see cref="CompanyPolicyRule.WaivePriorDayRequirement"/> — when enabled,
///      the employee is eligible regardless of attendance on the day before.
///   3. If <see cref="TimeAllowance.CheckAfterHoliday"/> is enabled, eligibility is
///      additionally conditioned on the employee having a qualifying day *after*
///      the holiday too (same search, run forward).
/// </summary>
public class LegalHolidayEligibilityEvaluator : IHolidayEligibilityEvaluator
{
    public bool Evaluate(TimeRange input, TimeContext context)
    {
        var payload = context.Payload;
        var cacheKey = SpecEvaluationCache.CreateKey<IsEligibleForHoliday>(context);

        var cached = payload.SharedSpecCache.GetByKey(cacheKey);
        if (cached.Found)
            return cached.Value;

        bool isEligible = WorkedHolidayItself(context)
            || context.Payload.Data.CompanyPolicy.WaivePriorDayRequirement
            || HasQualifyingDayLookingBack(context);

        //TODO get this setting from company db TimeAllowance.CheckAfterHoliday
        // NOTE: this guard intentionally sits here rather than at the top of Evaluate.
        // The lookback search above must still run inside nested ProcessLineAsync calls
        // (e.g. for consecutive holidays like Dec 25 + 26), so it can't be short-circuited
        // by the suppression flag. Only the *forward* check is suppressed during recursion,
        // to avoid forward-check ↔ forward-check cycles.
        if (context.Payload.Data.CompanyPolicy.CheckAfterHoliday && isEligible && !LegalHolidayForwardCheck.SuppressForwardCheck.Value)
            isEligible = HasQualifyingDayLookingForward(context);

        payload.SharedSpecCache.Record(cacheKey, isEligible);
        return isEligible;
    }

    /// <summary>
    /// An employee who actually worked enough of the holiday itself is eligible
    /// regardless of attendance on the surrounding days.
    /// </summary>
    private static bool WorkedHolidayItself(TimeContext context)
    {
        var minWorkingMinutes = context.Payload.Data.CurrentShift.MinimumWorkingMinutes;
        var workTime = context.Payload.Ledger.GetByTag("work_time", context);
        return workTime.TotalMinutes > 0 && workTime.TotalMinutes >= minWorkingMinutes;
    }

    private bool HasQualifyingDayLookingBack(TimeContext context)
    {
        var payload = context.Payload;
        var earliestDate = payload.Data.PayrollStartDate.AddDays(TimeAllowance.AttLookbackDays);
        for (var date = payload.Data.CurrentDate.AddDays(-1); date >= earliestDate; date = date.AddDays(-1))
        {
            var record = ProcessLineAsync(date, context).GetAwaiter().GetResult();
            if (record is null)
                continue;

            var verdict = ClassifyDay(record, context, lookingForward: false);
            if (verdict == DayVerdict.Inconclusive)
                continue;

            return verdict == DayVerdict.Qualifies;
        }

        return false;
    }

    private bool HasQualifyingDayLookingForward(TimeContext context)
    {
        var payload = context.Payload;
        var latestDate = payload.Data.CurrentDate.AddDays(TimeAllowance.AttLookforward);
        if (WorkedHolidayItself(context)) return true;
        // Suppress nested forward checks while recursing into ProcessLineAsync below —
        // otherwise a holiday-adjacent-to-a-holiday could trigger a forward check inside
        // a forward check. try/finally guarantees the flag clears even on early return,
        // so it never leaks into the next date processed by the outer caller's loop.
        LegalHolidayForwardCheck.SuppressForwardCheck.Value = true;
        try
        {
            for (var date = payload.Data.CurrentDate.AddDays(1); date <= latestDate; date = date.AddDays(1))
            {
                var record = ProcessLineAsync(date, context).GetAwaiter().GetResult();
                if (record is null) continue;
                var verdict = ClassifyDay(record, context, lookingForward: true);
                if (verdict == DayVerdict.Inconclusive) continue;
                return verdict == DayVerdict.Qualifies;
            }
            return false;
        }
        finally
        {
            LegalHolidayForwardCheck.SuppressForwardCheck.Value = false;
        }
    }

    private enum DayVerdict
    {
        /// <summary>Day doesn't decide eligibility either way — keep searching.</summary>
        Inconclusive,
        Qualifies,
        DoesNotQualify
    }

    /// <summary>
    /// Determines whether a single day (found while searching backward or forward
    /// from the holiday) settles eligibility, and if so, which way.
    ///
    /// NOTE ON ASYMMETRY: the forward search additionally treats
    /// <see cref="WorkType.RegularHoliday"/> as inconclusive (skip and keep looking);
    /// the backward search does not currently have that case. This mirrors the
    /// original implementation. If this is unintentional, it should be reconciled —
    /// flagging here rather than silently changing the behavior.
    /// </summary>
    private static DayVerdict ClassifyDay(DTRDetailModel record, TimeContext context, bool lookingForward)
    {
        var minWorkingHours = context.Payload.Data.CurrentShift.MinimumWorkingMinutes.ToHour();
        var workHours = record.TotalHours;

        if (workHours == 0 && record.WorkTypeEnum is WorkType.SpecialNonWorkingHoliday or WorkType.Skipped or WorkType.RestDay)
            return DayVerdict.Inconclusive;

        if (lookingForward && record.WorkTypeEnum == WorkType.LegalHoliday)
            return DayVerdict.Inconclusive;

        if (record.WorkTypeEnum == WorkType.Travel || record.WorkTypeEnum == WorkType.RestDayTravel)
            return DayVerdict.Qualifies;

        if (record.WorkTypeEnum == WorkType.RestDayDuty && minWorkingHours > workHours)
            return DayVerdict.Qualifies;

        if (record.HolCount > 0 || record.WorkTypeEnum is WorkType.PaidLeave)
            return DayVerdict.Qualifies;

        if (record.WorkTypeEnum == WorkType.SpecialNonWorkingHoliday)
            return DayVerdict.Inconclusive;

        return workHours > 0 && workHours >= minWorkingHours
            ? DayVerdict.Qualifies
            : DayVerdict.DoesNotQualify;
    }

    private static async Task<DTRDetailModel?> ProcessLineAsync(DateOnly date, TimeContext context)
    {
        var payload = context.Payload;
        var employee = payload.Data.Employee;
        var dtrService = payload.Provider.DtrContextModel.DtrService;

        if (dtrService is null)
            return null;

        var curPayload = dtrService.GetPayload(date);
        var result = await dtrService.GetDTRInfoAsync<DTRDetailModel>(curPayload, ProcessorType.DTRDetail, dtrService.GetToken, IncludeNullResponse.Include, true);
        return result.FirstOrDefault();
    }
}

public static class LegalHolidayForwardCheck
{
    public static readonly AsyncLocal<bool> SuppressForwardCheck = new();
}
