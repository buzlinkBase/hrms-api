using Hrms.Domain.Entities;

namespace Hrms.Core.Services.Schedules;

// A loan's amortization schedule. An installment is locked once payroll has touched it:
// referenced by a PayrollDeductionDetail (a generated run, posted or not), or already paid
// down at Post (Balance below Amount / Status "Paid" -- see PayrollService.ReduceDeductionBalancesAsync).
// Rebuilding Balance from Amount on an edit used to reset paid installments, so they were
// deducted again on the next run.
public sealed class LoanScheduleReconciler
    : ScheduleReconciler<DeductionApplication, DeductionApplicationDetail, UpdateDeductionApplicationDetail>
{
    private readonly IReadOnlySet<Guid> _usedByPayroll;

    /// <param name="usedByPayroll">Ids of installments some PayrollDeductionDetail references.</param>
    public LoanScheduleReconciler(IReadOnlySet<Guid> usedByPayroll)
    {
        _usedByPayroll = usedByPayroll;
    }

    public static readonly LoanScheduleReconciler ForNewLoan = new(new HashSet<Guid>());

    protected override Guid IdOf(UpdateDeductionApplicationDetail input) => input.Id;

    // DeductionId == Guid.Empty marks a stray row Mapster used to copy in alongside the real ones
    // (Balance 0, no employee/deduction); payroll never read it, so it isn't "paid".
    // A row an attendance rule already skipped or deferred was judged by a saved payroll run, so it
    // is as final as a paid one.
    protected override bool IsLocked(DeductionApplicationDetail row) =>
        _usedByPayroll.Contains(row.Id) || row.IsSkipped || row.IsDeferred ||
        (row.DeductionId != Guid.Empty && (row.Balance != row.Amount || row.Status == "Paid"));

    protected override bool IsUnchanged(DeductionApplicationDetail row, UpdateDeductionApplicationDetail input) =>
        row.Date == input.Date && row.Principal == input.Principal &&
        row.Interest == input.Interest && row.Amount == input.Amount &&
        row.PeriodStart == input.PeriodStart && row.PeriodEnd == input.PeriodEnd;

    protected override void Apply(DeductionApplication parent, DeductionApplicationDetail row,
        UpdateDeductionApplicationDetail input, int position)
    {
        row.Date = input.Date;
        row.Principal = input.Principal;
        row.Interest = input.Interest;
        row.Amount = input.Amount;
        row.Balance = input.Amount;
        row.PeriodStart = input.PeriodStart;
        row.PeriodEnd = input.PeriodEnd;
        row.RecordOrder = position + 1;
        row.EmployeeId = parent.EmployeeId;
        row.DeductionId = parent.DeductionId;
    }

    protected override DeductionApplicationDetail Create(DeductionApplication parent,
        UpdateDeductionApplicationDetail input, int position)
    {
        var row = new DeductionApplicationDetail();
        Apply(parent, row, input, position);
        return row;
    }

    protected override DateOnly DateOf(DeductionApplicationDetail row) => row.Date;
}
