using Hrms.Domain.Entities.EmployeeEntities;

namespace Hrms.Domain.Entities;

// One continuously-growing balance per employee for the life of their employment -- unlike
// LeaveCredits, Retirement isn't a per-calendar-year-reset entitlement, so there's no
// PeriodYear/FromDate/ToDate here. Balance only ever moves at Post time (see
// PayrollService.ProcessRetirementFundActivityAsync) -- a regenerated or deleted payroll draft
// never touches it. Payout at separation is wired through Last Pay (LastPayrollService
// .GenerateAsync's IncludeRetirementPayout option, settled at Post via the same method above).
public class RetirementFund : BaseEntity
{
    public Guid EmployeeId { get; set; }
    public virtual Employee Employee { get; set; } = null!;
    public decimal Balance { get; set; }
}

// Setup > Payroll Reports > Retirement Ledger -- Accrual/Payout are written automatically
// (PayrollService.ProcessRetirementFundActivityAsync); Adjustment is a manual HR entry (see
// PayrollService.AdjustRetirementBalanceAsync) -- e.g. seeding an opening balance when a tenant
// starts using this system mid-year, before this employee's history of accruals exists here.
public enum RetirementLedgerEntryType
{
    Accrual,
    Adjustment,
    Payout,
}

// Append-only audit trail of every accrual/payout/adjustment against a RetirementFund, mirroring
// LeaveLedger's Add/Less shape.
public class RetirementLedger : BaseEntity
{
    public Guid EmployeeId { get; set; }
    public virtual Employee Employee { get; set; } = null!;
    public Guid RetirementFundId { get; set; }
    public virtual RetirementFund RetirementFund { get; set; } = null!;
    public RetirementLedgerEntryType EntryType { get; set; }
    // Nullable -- only Accrual/Payout entries originate from an actual payroll run. A manual
    // Adjustment entry (see EntryType above) has no Payroll to point to.
    public Guid? PayrollId { get; set; }
    public virtual Payroll? Payroll { get; set; }
    public DateOnly EntryDate { get; set; }
    public decimal Add { get; set; }
    public decimal Less { get; set; }
    public decimal Balance { get; set; }   // running balance after this entry
    public string Particulars { get; set; } = string.Empty;
}
