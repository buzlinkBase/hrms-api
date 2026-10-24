namespace Hrms.Domain.ValueObjects;

// Payroll Reports > Cash Bond Tracking -- manual "Add entry" / "Adjust" action, mirroring
// AdjustRetirementPayload. An additive delta + direction (IsAddition), not "set a new total".
// Typically the opening balance of cash bond already deducted before this system was adopted.
public record AdjustCashBondPayload(
    Guid EmployeeId,
    decimal Amount,
    bool IsAddition,
    string Particulars,
    DateOnly? EntryDate = null);

// Payroll Reports > Cash Bond Tracking > "Edit rate" -- sets the per-run cash bond (Employee.CashBond).
// Applies from the next payroll run; logged as a CashBondRateChange without touching the collected total.
public record ChangeCashBondRatePayload(Guid EmployeeId, decimal NewRate, string Reason);
