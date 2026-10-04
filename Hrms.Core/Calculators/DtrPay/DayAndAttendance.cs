namespace Hrms.Core.Calculators.DtrPay;

// Absences, lates, undertime and leave for the day -- identical in both pay methods.
internal interface IAttendancePayCalculator
{
    AttendancePay Calculate(PayrollContext context);
}

internal sealed class AttendancePayCalculator : IAttendancePayCalculator
{
    private readonly AbsentPipeline _absent;
    private readonly LatesPipeLine _lates;
    private readonly UnderTimePipeLine _undertime;
    private readonly LeavePipeline _leave;

    public AttendancePayCalculator(AbsentPipeline absent, LatesPipeLine lates, UnderTimePipeLine undertime, LeavePipeline leave)
    {
        _absent = absent;
        _lates = lates;
        _undertime = undertime;
        _leave = leave;
    }

    public AttendancePay Calculate(PayrollContext context)
    {
        var leave = _leave.Run(context);
        return new AttendancePay(
            Absent: _absent.Run(context).Value,
            Late: _lates.Run(context).Value,
            Undertime: _undertime.Run(context).Value,
            PaidLeave: leave.Where(x => x.PayType == PayType.WithPay).Sum(x => x.Value),
            UnpaidLeave: leave.Where(x => x.PayType == PayType.WithoutPay).Sum(x => x.Value));
    }
}
