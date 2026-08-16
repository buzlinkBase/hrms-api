using Hrms.Domain.Entities;

namespace DTR.Core;

public class AppliedOvertimePolicy : ConditionalPolicyBase
{
    public AppliedOvertimePolicy(IRuleSpecification specification)
        : base(specification, SpecFailureBehavior.ReturnEmpty) { }

    protected override TimeRange ApplyIfSatisfied(TimeRange input, TimeContext context)
    {
        var shift = context.Payload.Data.CurrentShift;
        var ledgerKey = this.CreateLedgerKey(context);

        // 🧾 1. Check cache for existing claim
        var cached = context.Payload.Ledger.GetByKey(ledgerKey);
        if (cached.Found) return cached.Value ?? TimeRange.Empty;

        // 📋 2. Pull applied OT data
        var appliedOT = context.Payload.Provider.OTProvider.GetOT(shift.ShiftDate);
        if (appliedOT == null) return TimeRange.Empty;

        // 🛑 3. Check for system auto-computed OT
        var systemKey = TimeRangeLedger.CreateKey<AutoComputeOvertimePolicy>(context);
        var cachedSystem = context.Payload.Ledger.GetByKey(systemKey);
        var systemOT = cachedSystem.Value ?? TimeRange.Empty;

        // 🔄 4. Calculate OT result based on whether system OT exists
        var result = !systemOT.IsEmpty()
            ? GetIntersectedOT(systemOT, appliedOT)
            : CalcAppliedOT(context, ledgerKey, appliedOT);

        this.RecordLedger(context, result);
        return result;
    }

    private static TimeRange GetIntersectedOT(TimeRange systemOT, OverTimeApplication appliedOT)
    {
        if (appliedOT.IsManualEntry)
        {
            var retagged = systemOT.TimeRecords
                .CropFromStart(appliedOT.ManualOTMinutes)
                .TimeRecords
                .Retag("OT_ManualOverride")
                .ToTimeRange();

            return retagged.TotalMinutes >= appliedOT.OverTimeThreshold ? retagged : TimeRange.Empty;
        }

        var otTimeBlock = TryCreateOTTimeBlock(appliedOT);
        if (otTimeBlock != null)
        {
            return systemOT.TimeRecords.Intersect(otTimeBlock, "OT_applied").ToTimeRange();
        }
        return TimeRange.Empty;
    }

    private TimeRange CalcAppliedOT(TimeContext context, TimeRangeLedgerCacheKey ledgerKey, OverTimeApplication appliedOT)
    {
        var shift = context.Payload.Data.CurrentShift;
        var blocked = context.Payload.Ledger.GetAllAllocatedExcept(ledgerKey);
        var usable = context.CanonicalTimeRange.TimeRecords
            .Exclude(blocked)
            .MergeOverlapping();

        var fallbackSlices = TimeRange.Empty;

        if (appliedOT.IsManualEntry)
        {
            var otStart = GetOTStartTime(context);
            fallbackSlices = usable
                .Where(r => r.StartTime >= otStart && r.StartTime >= shift.EndTime)
                .Select(r => new TimeRecord(r.StartTime, r.EndTime, "OT_applied"))
                .ToTimeRecordCollection()
                .CropFromStart(appliedOT.ManualOTMinutes);
        }
        else
        {
            var otTimeBlock = TryCreateOTTimeBlock(appliedOT);
            if (otTimeBlock != null)
            {
                fallbackSlices = usable.Intersect(otTimeBlock, "OT_applied").ToTimeRange();
            }
        }

        return fallbackSlices.TotalMinutes >= shift.OverTimeThreshold ? fallbackSlices : TimeRange.Empty;
    }

    private DateTime GetOTStartTime(TimeContext context)
    {
        var shift = context.Payload.Data.CurrentShift;

        if (shift.ShiftType == TimeShiftType.SPLIT)
        {
            var regKey = TimeRangeLedger.CreateKey<RegularHourPolicy>(context);
            var allocated = context.Payload.Ledger.GetAllAllocatedExcept(regKey);
            var usable = context.CanonicalTimeRange.TimeRecords.ExcludeLeave().Exclude(allocated);

            return usable.MinBy(x => x.StartTime)?.StartTime.AddMinutes(shift.MaxWorkingMinutes)
                ?? shift.EndTime;
        }

        return shift.StartTime.AddMinutes(shift.MaxWorkingMinutes + TimeAllowance.OTTimeCaptureAllowanceMinutes);
    }

    private static TimeRecordCollection? TryCreateOTTimeBlock(OverTimeApplication appliedOT)
    {
        if (!appliedOT.StartTime.HasValue || !appliedOT.EndTime.HasValue) return null;

        return new TimeRecordCollection
        {
            new TimeRecord
            {
                StartTime = appliedOT.StartTime.Value,
                EndTime = appliedOT.EndTime.Value
            }
        };
    }
}