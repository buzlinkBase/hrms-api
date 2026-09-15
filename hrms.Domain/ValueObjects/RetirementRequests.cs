namespace Hrms.Domain.ValueObjects;

// Payroll Reports > Retirement Ledger — manual "Adjust Balance" action, mirroring
// AdjustUniformAllowancePayload exactly. An additive delta + direction (IsAddition), not "set a
// new balance". Useful for seeding an opening balance when this system is adopted mid-year.
public record AdjustRetirementPayload(
    Guid EmployeeId,
    decimal Amount,
    bool IsAddition,
    string Particulars,
    DateOnly? EntryDate = null);
