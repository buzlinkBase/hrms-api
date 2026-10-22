using Hrms.Domain.Entities.EmployeeEntities;

namespace Hrms.Domain.Entities;

// One continuously-growing balance per employee for the life of their employment (same
// single-lifetime-balance shape as RetirementFund -- no PeriodYear/FromDate/ToDate, unlike
// LeaveCredits). Grows monthly via UniformAllowanceAccrualWorker, and can also be adjusted or
// released (disbursed) manually by HR -- see UniformAllowanceFundService.
public class UniformAllowanceFund : BaseEntity, IEmployeeOwned, IEntryRecord
{
    public Guid EmployeeId { get; set; }
    public virtual Employee Employee { get; set; } = null!;
    public decimal Balance { get; set; }

    // When service counts from for the waiting period and anniversary credits. Null = the hire
    // date. Moves on a rehire unless the client's policy bridges the gap (UniformRehireRule).
    public DateOnly? ServiceStartDate { get; set; }
    // Set when the accrual run first sees the employee separated, cleared when it sees them back
    // -- the app keeps no rehire record, so this is how a rehire is told apart.
    public DateOnly? SeparatedOn { get; set; }
}

// Deliberately its own enum rather than reusing Leave's LedgerEntryType -- that enum's
// `Released` value already means "a DTR-driven soft hold was lifted," not "paid out to the
// employee" (see hrms.Domain/Enums.cs), and most of its other values (Grant/CarryOver/Reserved/
// Deduction/Expiry) are DTR/period concepts that don't apply to this single-balance model.
public enum UniformAllowanceEntryType
{
    Accrual,      // UniformAllowanceAccrualWorker's scheduled credit
    Adjustment,   // manual HR add/remove correction
    Release,      // HR disburses (issues uniforms/cash for) accumulated balance for a period
    Forfeiture,   // balance written off on separation (UniformSeparationRule.Forfeit)
    Expiry,       // balance lapsed under the policy's reset/expiry rule
}

// Append-only audit trail of every accrual/adjustment/release against a UniformAllowanceFund,
// mirroring LeaveLedger/RetirementLedger's signed Amount/Balance shape.
public class UniformAllowanceLedger : BaseEntity, IEmployeeOwned, IEntryRecord
{
    public Guid EmployeeId { get; set; }
    public virtual Employee Employee { get; set; } = null!;
    public Guid UniformAllowanceFundId { get; set; }
    public virtual UniformAllowanceFund UniformAllowanceFund { get; set; } = null!;
    public UniformAllowanceEntryType EntryType { get; set; }
    public DateOnly EntryDate { get; set; }
    // Signed pesos: + accrual / adjustment added, − release / adjustment removed.
    public decimal Amount { get; set; }
    public decimal Balance { get; set; }   // running balance after this entry
    public string Particulars { get; set; } = string.Empty;

    // Accruals: the policy version and attendance window that earned it (null on older rows and
    // on manual entries).
    public Guid? PolicyId { get; set; }
    public DateOnly? PeriodStart { get; set; }
    public DateOnly? PeriodEnd { get; set; }

    // Multi-instance safety net for UniformAllowanceAccrualWorker's own entries: Accrual rows
    // carry "{EmployeeId}|{yyyy-MM}" (at most one credit a month whatever the frequency) and
    // Expiry rows "{EmployeeId}|expiry|{yyyy-MM}"; manual Adjustment/Release rows leave it null.
    // The unique index on this column is the hard stop if two instances/redeliveries race past
    // the worker's own pre-checks -- multiple nulls never collide.
    public string? AccrualDedupeKey { get; set; }
}
