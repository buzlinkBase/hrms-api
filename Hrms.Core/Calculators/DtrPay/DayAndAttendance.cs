namespace Hrms.Core.Calculators.DtrPay;

// Prices a category's plain worked/unworked hours through its existing day-type pipeline (rest-day
// and holiday multipliers, Fixed-salary pre-funding, unworked holiday pay). Shared unchanged by
// both pay methods -- they only differ in how OT / ND / NDOT hours are priced.
internal interface IDayTypePayPricer
{
    DayPay Price(PayCategoryKind kind, PayrollContext context);
}

internal sealed class DayTypePayPricer : IDayTypePayPricer
{
    private readonly IReadOnlyDictionary<PayCategoryKind, BasicPipelineBase> _pipelines;

    public DayTypePayPricer(
        RegularPipeline regular,
        RestDayPipeLine restDay,
        RegularHolidayPipeLine legal,
        SpecialWorkDayPipeLine special,
        RestLegalDayPipeLine restLegal,
        RestSpecialDayPipeLine restSpecial,
        DoubleLegalPipeLine doubleLegal,
        RestDoubleLegalPipeLine restDoubleLegal)
    {
        _pipelines = new Dictionary<PayCategoryKind, BasicPipelineBase>
        {
            [PayCategoryKind.Regular] = regular,
            [PayCategoryKind.RestDay] = restDay,
            [PayCategoryKind.Legal] = legal,
            [PayCategoryKind.Special] = special,
            [PayCategoryKind.RestLegal] = restLegal,
            [PayCategoryKind.RestSpecial] = restSpecial,
            [PayCategoryKind.DoubleLegal] = doubleLegal,
            [PayCategoryKind.RestDoubleLegal] = restDoubleLegal,
        };
    }

    public DayPay Price(PayCategoryKind kind, PayrollContext context)
    {
        var line = _pipelines[kind].Run(context);
        return new DayPay(line.Value, line.Worked, line.UnWork);
    }
}

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

// Additive folds night hours into the day bucket -- except where the day bucket wouldn't pay them.
// A Fixed-salary employee's Basic Pay is the monthly salary, not the Regular category's day pay
// (EmployeePayrollLineService.GetBasicPay), while their Regular night hours' base has always been
// paid on top of it through the ND bucket. Moving it into the day bucket would silently drop it,
// so for them it stays in the ND bucket ({Cat}NDBasePay), exactly where it always was.
internal interface INdBaseRoutingRule
{
    bool KeepInNdBucket(PayCategory category, PayrollContext context);
}

internal sealed class FixedSalaryRegularNdBaseRule : INdBaseRoutingRule
{
    public bool KeepInNdBucket(PayCategory category, PayrollContext context) =>
        category.Kind == PayCategoryKind.Regular && context.Employee.SalaryType == SalaryType.FIXED;
}
