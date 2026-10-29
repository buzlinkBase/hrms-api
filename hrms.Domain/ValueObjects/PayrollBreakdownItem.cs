namespace Hrms.Domain.ValueObjects;

// One deduction or Other Income type's share of a saved payroll, for the Payroll Summary's
// per-item columns (screen, print-out and exports). ItemId is the Deduction/OtherIncome type id,
// so the same type lines up across employees even if it was later renamed. IsLoan is only
// meaningful for deductions (the "LOAN"-coded categories behind Payroll.TotalLoans).
public record PayrollBreakdownItem(Guid ItemId, string Label, decimal Amount, bool IsLoan = false);

// One itemized line of a saved payroll amount (the Payroll Summary's breakdown popup), the saved-run
// twin of the preview's PayrollSourceItem: the deduction / income type's name plus the day that
// installment was scheduled for.
public record PayrollSourceRow(string Label, decimal Amount, string? Note)
{
    public static PayrollSourceRow Scheduled(string label, decimal amount, DateOnly? date) =>
        new(label, amount, date is { } d ? $"Scheduled {d:MMM d, yyyy}" : null);
}
