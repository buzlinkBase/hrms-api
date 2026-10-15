using Hrms.Domain.Entities;

namespace Hrms.Core.Services.Schedules;

// Payroll (IncomeAplDtlService.LoadAsync) reads schedule rows on their own, never the parent,
// so every value it needs -- employee, income, taxable -- is copied onto each row.
// A row is locked once a saved payroll run consumed it (ConsumedByPayrollId).
public sealed class OtherIncomeScheduleReconciler
    : ScheduleReconciler<OtherIncomeApplication, OtherIncomeSchedules, UpdateOtherIncomeSchedule>
{
    public static readonly OtherIncomeScheduleReconciler Instance = new();

    private OtherIncomeScheduleReconciler() { }

    public static void Inherit(OtherIncomeApplication application, OtherIncomeSchedules row)
    {
        row.EmployeeId = application.EmployeeId;
        row.IncomeId = application.IncomeId;
        row.IsTaxable = application.IsTaxable;
    }

    protected override Guid IdOf(UpdateOtherIncomeSchedule input) => input.Id;

    protected override bool IsLocked(OtherIncomeSchedules row) => row.ConsumedByPayrollId != null;

    protected override bool IsUnchanged(OtherIncomeSchedules row, UpdateOtherIncomeSchedule input) =>
        row.Date == input.Date && row.Amount == input.Amount
        && row.PeriodStart == input.PeriodStart && row.PeriodEnd == input.PeriodEnd;

    protected override void Apply(OtherIncomeApplication parent, OtherIncomeSchedules row,
        UpdateOtherIncomeSchedule input, int position)
    {
        row.Date = input.Date;
        row.Amount = input.Amount;
        row.PeriodStart = input.PeriodStart;
        row.PeriodEnd = input.PeriodEnd;
        Inherit(parent, row);
    }

    protected override OtherIncomeSchedules Create(OtherIncomeApplication parent,
        UpdateOtherIncomeSchedule input, int position)
    {
        var row = new OtherIncomeSchedules
        {
            Date = input.Date,
            Amount = input.Amount,
            PeriodStart = input.PeriodStart,
            PeriodEnd = input.PeriodEnd,
        };
        Inherit(parent, row);
        return row;
    }

    protected override DateOnly DateOf(OtherIncomeSchedules row) => row.Date;
}
