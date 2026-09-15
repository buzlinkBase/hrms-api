namespace Hrms.Domain.ValueObjects;

// Setup > Client > Settings > Allowances > Uniform Allowance Ledger report — manual "Adjust
// Balance" action. An additive delta + direction (IsAddition), not "set a new balance".
public record AdjustUniformAllowancePayload(
    Guid EmployeeId,
    decimal Amount,
    bool IsAddition,
    string Particulars,
    DateOnly? EntryDate = null);

// One employee's share of a batch Release — Amount is editable per employee in the review step
// before submitting, defaulting to (but not forced to) their current balance.
public record UniformAllowanceReleaseItem(Guid EmployeeId, decimal Amount);

public record ReleaseUniformAllowancePayload(
    List<UniformAllowanceReleaseItem> Releases,
    DateOnly PeriodDate,
    string Particulars);
