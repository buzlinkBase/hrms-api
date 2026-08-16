namespace DTR.Core;

public class PostShiftOTUnrestrictedHandler : PostShiftOTHandler
{
    public TimeRange Calculate(TimeRange input, TimeContext context) => Process(input, context);
}
public class PostShiftOTHandler : OTComputationHandlerBase
{
    protected override bool CanHandle(TimeRange input, TimeContext context)
    {
        if (context.Payload.Data.Employee.ClientId.HasValue)
        {
            var clientpolicy = context.Payload.Provider.ClientPolicyProvider.GetPolicy(context.Payload.Data.Employee.ClientId.Value);
            if (clientpolicy != null)
            {
                return clientpolicy.OvertimeInclusionPolicy == OvertimeInclusionPolicy.UsePostShiftWork;
            }
        }
        return context.Payload.Data.CompanyPolicy.OTInclusionPolicy == OvertimeInclusionPolicy.UsePostShiftWork;
    }

    protected override TimeRange Process(TimeRange input, TimeContext context)
    {

        var shift = context.Payload.Data.CurrentShift;
        var ledgerKey = TimeRangeLedger.CreateKey(nameof(PostShiftOTHandler), context);
        var otStart = AutoComputeOTStartFactory.Create(context).GetOTStartTime();
        if (!otStart.HasValue)
        {
            context.Payload.Ledger.Record(ledgerKey, TimeRange.Empty);
            return TimeRange.Empty;
        }
        otStart = new DateTime(otStart.Value.Year, otStart.Value.Month, otStart.Value.Day, otStart.Value.Hour, otStart.Value.Minute, otStart.Value.Second);
        var blocked = context.Payload.Ledger
            .GetAllAllocatedExcept(ledgerKey)
            ;
        var usable = context.CanonicalTimeRange.TimeRecords
            .ExcludeLeave()
            .Exclude(blocked)
            .MergeOverlapping();

        var withCaptureAllowance = otStart.Value.AddMinutes(TimeAllowance.OTTimeCaptureAllowanceMinutes);
        var postShiftOT = usable
            .Where(r => r.StartTime >= withCaptureAllowance)
            .Select(r => r.Tag("OT_auto"))
            .ToTimeRecordCollection()
            .ToTimeRange();


        if (!postShiftOT.TimeRecords.Any()) return TimeRange.Empty;
        var otOut = postShiftOT.TimeRecords.MaxBy(x => x.EndTime)?.EndTime;
        if (otOut == null) return TimeRange.Empty;

        var OTshift = new CurrentShift() { StartTime = otStart.Value, EndTime = otOut.Value };

        var tr = new TimeRecordCollection()
        {
            new TimeRecord(OTshift.StartTime,OTshift.EndTime)
        };
        var OTIntersectedTime = postShiftOT.TimeRecords.Intersect(tr);
        var finalRange = TimeRange.Set(OTIntersectedTime.TotalMinutes(), OTIntersectedTime);
        context.Payload.Ledger.Record(ledgerKey, finalRange);
        return finalRange;

    }
}
public class AutoComputeOTStartFactory
{
    public static IOTStartProvider Create(TimeContext context)
    {
        var shift = context.Payload.Data.CurrentShift;
        if (shift.OTRequireTimeIn)
        {
            return new AutoComputeOTTimeInProvider(context);
        }
        else if (!shift.OTRequireTimeIn)
        {
            return new AutoComputeOTTimeInNotRequiredProvider(context);
        }
        throw new NotImplementedException();
    }
}
public class AutoComputeOTTimeInProvider : IOTStartProvider
{
    private readonly TimeContext _context;

    public AutoComputeOTTimeInProvider(TimeContext context)
    {
        _context = context;
    }
    public DateTime? GetOTStartTime()
    {
        var _shift = _context.Payload.Data.CurrentShift;

        if (!_shift.OTRequireTimeIn
          || _shift.OTStartTime == null
          || !_shift.OTStartTime.HasValue) return null;

        var date = _shift.EndTime.Date;
        var baseOTStart = date.Add(_shift.OTStartTime.Value);
        //with 15mins allowance from timeshift end
        var allowedStart = baseOTStart;
        return allowedStart;
    }
}
public class AutoComputeOTTimeInNotRequiredProvider : IOTStartProvider
{
    private readonly TimeContext _context;

    public AutoComputeOTTimeInNotRequiredProvider(TimeContext context)
    {
        _context = context;
    }
    public DateTime? GetOTStartTime()
    {

        var NotRequiredProcessor = OTTimeInNotRequiredProviderFactory.Create(_context);
        return NotRequiredProcessor.GetOTStartTime();
    }
}
public class OTTimeInNotRequiredProviderFactory
{
    public static IShiftTypeIdentifier Create(TimeContext context)
    {
        switch (context.Payload.Data.CurrentShift.ShiftType)
        {
            case TimeShiftType.FIXED://base on timeshift
                return new FixedOT(context);
            case TimeShiftType.SPLIT://based on regularClaimed caping
                return new SplitOT(context);
            default:
                throw new NotImplementedException("IShiftTypeIdentifier");
        }
    }
}
public class FixedOT : IShiftTypeIdentifier
{
    private readonly TimeContext _context;
    public FixedOT(TimeContext context)
    {
        _context = context;
    }
    public DateTime? GetOTStartTime()
    {
        var shift = _context.Payload.Data.CurrentShift;
        var start = shift.StartTime.AddMinutes(shift.MaxWorkingMinutes);
        //var date = _context.Payload.Data.CurrentShift.EndTime;
        return start;
    }
}
public class SplitOT : IShiftTypeIdentifier
{
    private readonly TimeContext _context;

    public SplitOT(TimeContext context)
    {
        _context = context;
    }
    public DateTime? GetOTStartTime()
    {
        //starttime is the shift end time or starttime after the regularhour was claimed
        var shift = _context.Payload.Data.CurrentShift;
        var regKey = TimeRangeLedger.CreateKey<RegularHourPolicy>(_context);
        var data = _context.Payload.Ledger.GetAllAllocatedExcept(regKey);
        var usable = _context.CanonicalTimeRange.TimeRecords.ExcludeLeave().Exclude(data);
        var startTime = usable.MinBy(x => x.StartTime)?.StartTime.AddMinutes(shift.MaxWorkingMinutes); // ?? _context.Payload.Data.CurrentShift.EndTime;
        return startTime;
    }
}
public interface IShiftTypeIdentifier
{
    DateTime? GetOTStartTime();
}
public interface IOTStartProvider
{
    DateTime? GetOTStartTime();
}