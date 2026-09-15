using Hrms.Domain.Entities.EmployeeEntities;

namespace Hrms.Domain.Entities;

// One continuously-growing balance per employee for the life of their employment (same
// single-lifetime-balance shape as RetirementFund -- no PeriodYear/FromDate/ToDate, unlike
// LeaveCredits). Grows monthly via UniformAllowanceAccrualWorker, and can also be adjusted or
// released (disbursed) manually by HR -- see UniformAllowanceFundService.
public class UniformAllowanceFund : BaseEntity
{
    public Guid EmployeeId { get; set; }
    public virtual Employee Employee { get; set; } = null!;
    public decimal Balance { get; set; }
}

// Deliberately its own enum rather than reusing Leave's LedgerEntryType -- that enum's
// `Released` value already means "a DTR-driven soft hold was lifted," not "paid out to the
// employee" (see hrms.Domain/Enums.cs), and most of its other values (Grant/CarryOver/Reserved/
// Deduction/Expiry) are DTR/period concepts that don't apply to this single-balance model.
public enum UniformAllowanceEntryType
{
    Accrual,      // UniformAllowanceAccrualWorker's monthly credit
    Adjustment,   // manual HR add/remove correction
    Release,      // HR disburses (issues uniforms/cash for) accumulated balance for a period
}

// Append-only audit trail of every accrual/adjustment/release against a UniformAllowanceFund,
// mirroring LeaveLedger/RetirementLedger's Add/Less/Balance shape.
public class UniformAllowanceLedger : BaseEntity
{
    public Guid EmployeeId { get; set; }
    public virtual Employee Employee { get; set; } = null!;
    public Guid UniformAllowanceFundId { get; set; }
    public virtual UniformAllowanceFund UniformAllowanceFund { get; set; } = null!;
    public UniformAllowanceEntryType EntryType { get; set; }
    public DateOnly EntryDate { get; set; }
    public decimal Add { get; set; }
    public decimal Less { get; set; }
    public decimal Balance { get; set; }   // running balance after this entry
    public string Particulars { get; set; } = string.Empty;
}
