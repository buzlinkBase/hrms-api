using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DTR.Core.DTR.Rules.Policies;

public class TravelPolicy : ConditionalPolicyBase
{
    private readonly IRuleSpecification _spec;
    public TravelPolicy(IRuleSpecification spec) : base(spec, SpecFailureBehavior.ReturnEmpty)
    {
        _spec = spec;
    }
    protected override TimeRange ApplyIfSatisfied(TimeRange input, TimeContext context)
    {
        var application = context.Payload.Data.CurrentTravel;
        if (application == null || !application.StartTime.HasValue || !application.EndTime.HasValue)
        {
            return TimeRange.Empty;
        }

        var MaxWorkingMinutes = context.Payload.Data.CurrentShift.MaxWorkingMinutes;

        if (application.IsManualEntry)
        {
            var resultRange = new TimeRange(Math.Min(application.TotalMinutes, MaxWorkingMinutes));
            return resultRange;
        }

        var workHours = context.Payload.Ledger.GetByTag("work_time",context);
        var timeBlock = new TimeRecordCollection()
        {
            new TimeRecord
            {
                StartTime=application.StartTime.Value,
                EndTime=application.EndTime.Value,
            }
        };

        //cap to timeshift
        var capped = timeBlock.CapAndCrop(context.Payload.Data.CurrentShift);
        var rangeResult = capped.TimeRecords.Intersect(workHours.TimeRecords).ToTimeRange();
        return rangeResult;
    }
}
