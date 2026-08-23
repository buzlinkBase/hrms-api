namespace DTR.Core;

public class UndertimePolicy : ConditionalPolicyBase
{
    private readonly IRuleSpecification _specification;
    public UndertimePolicy(IRuleSpecification specification) : base(specification)
    {
        _specification = specification;
    }
    protected override TimeRange ApplyIfSatisfied(TimeRange input, TimeContext context)
    {
        var payload = context.Payload;
        var shift = payload.Data.CurrentShift;
        var ledger = payload.Ledger;

        var ledgerKey = TimeRangeLedger.CreateKey("undertime", context);
        var cached = ledger.GetByKey(ledgerKey);
        if (cached.Found)
            return cached.Value ?? TimeRange.Empty;

        if (context.CanonicalTimeRange.TotalMinutes == 0)
        {
            ledger.Record(ledgerKey, TimeRange.Empty);
            return TimeRange.Empty;
        }

        var maxMinutes = shift.MaxWorkingMinutes;
        var finalReg = ledger.GetByTag("work_time", context);
        var finalLate = ledger.GetByTag("late", context);

        //if travel and leave providing hours only

        var currentTravel = context.Payload.Data.CurrentTravel;
        var currentLeave = context.Payload.Data.CurrentLeaves;

        var travelTime = currentTravel != null && currentTravel.IsManualEntry
            ? ledger.GetByTag("travel", context).TotalMinutes
            : 0;

        var leaveTime = ledger.GetByTag("onleave", context).TotalMinutes;
        var utMinutes = Math.Max(0, maxMinutes - travelTime - leaveTime - finalReg.TotalMinutes - finalLate.TotalMinutes);
        var result = utMinutes > 0 ? new TimeRange(utMinutes) : TimeRange.Empty;
        ledger.Record(ledgerKey, result);
        return result;

    }
}