namespace DTR.Core;

public static class LeaveAttendanceStrategyFactory
{
    private static readonly ILeaveAttendanceStrategy _singleDay = new SingleDayLeaveAttendanceStrategy();
    private static readonly ILeaveAttendanceStrategy _multiDay  = new MultiDayLeaveAttendanceStrategy();
    private static readonly ILeaveAttendanceStrategy _partial   = new PartialLeaveAttendanceStrategy();
    public static ILeaveAttendanceStrategy Create(DurationType durationType) => durationType switch
    {
        DurationType.SingleDay => _singleDay,
        DurationType.MultiDay  => _multiDay,
        DurationType.Partial   => _partial,
        _                      => _partial
    };
}
