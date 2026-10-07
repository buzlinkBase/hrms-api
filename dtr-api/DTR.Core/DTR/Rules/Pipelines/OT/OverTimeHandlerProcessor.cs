namespace DTR.Core;

public class OverTimeHandlerProcessor
{
    private readonly TimeContext _context;
    public OverTimeHandlerProcessor(TimeContext context)
    {
        _context = context;
    }
    public TimeRange Handle(TimeRange canonicalTimeRange)
    {
        var otTimeRange = OvertimeCapper.Cap(ComputeUncapped(canonicalTimeRange), _context);

        _context.Payload.Ledger.RecordByTag("OT", _context, otTimeRange);
        return otTimeRange;
    }

    // Acceptable OT (non-OT time already excluded) before the shift's Max OT Hours cap.
    // First8HrPolicy borrows its regular-time top-up from this, then caps what remains.
    public TimeRange ComputeUncapped(TimeRange canonicalTimeRange)
    {
        // Capture system OT first (needed for override OT to work)
        var systemHandler = new AutoComputedOTHandler(canonicalTimeRange, _context);
        systemHandler.Handle();

        // Run approval-based OT calc, chaining to system handler
        var appliedHandler = new ApprovalBasedOTHandler(canonicalTimeRange, _context);
        appliedHandler.SetNext(systemHandler);

        return OvertimeCapper.ExcludeNonOvertime(appliedHandler.Handle(), _context);
    }
}

