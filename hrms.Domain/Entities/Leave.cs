using Hrms.Domain.Entities.EmployeeEntities;

namespace Hrms.Domain.Entities;

public class Leave : BaseEntity
{
    // ── Identification ────────────────────────────────────────────────────────
    public string Code { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string Description { get; set; } = string.Empty;
    public string LegalBasis { get; set; } = string.Empty;
    public string Remarks { get; set; } = string.Empty;

    // ── Pay & Source ──────────────────────────────────────────────────────────
    public PaySource PaySource { get; set; } = PaySource.Company;
    public bool EmployerAdvancesPayment { get; set; }

    // ── Accrual ───────────────────────────────────────────────────────────────
    public AccrualBasis AccrualBasis { get; set; } = AccrualBasis.None;
    public double Credits { get; set; }               // lump-sum entitlement (AccrualBasis.None/PerEvent)
    public double AccrualRate { get; set; }           // days earned per accrual period
    public double? MaxAccrualBalance { get; set; }    // null = uncapped
    public bool ProRateFirstYear { get; set; }
    public LeaveReset LeaveReset { get; set; } = LeaveReset.PerPeriod;

    // ── Eligibility ───────────────────────────────────────────────────────────
    public LeaveEligibilityBasis EligibilityBasis { get; set; } = LeaveEligibilityBasis.TenureMonths;
    public int MinServiceMonths { get; set; }         // used when EligibilityBasis == TenureMonths; 0 = immediately eligible
    public int MinPresentDays { get; set; }           // used when EligibilityBasis == PresentDays; 0 = immediately eligible
    public GenderRestriction GenderRestriction { get; set; } = GenderRestriction.None;
    public bool RequiresApproval { get; set; } = true;
    public bool RequiresSupportingDocument { get; set; }
    public bool AllowEmployeeFiling { get; set; } = true; // false = hidden from Employee Portal, admin-only

    // ── Application Rules ─────────────────────────────────────────────────────
    public bool AllowHalfDay { get; set; } = true;
    public bool AllowPartial { get; set; }            // time-based (hours) leave
    public bool AllowNegativeBalance { get; set; }    // advance leave
    public bool RequiresCredits { get; set; } = true; // false = never credit-tracked (skip balance check)
    public double? MaxDaysPerYear { get; set; }       // annual cap (null = unlimited)
    public int? MaxConsecutiveDays { get; set; }      // per-application cap

    // ── Carry-Over ────────────────────────────────────────────────────────────
    public CarryOverType CarryOverType { get; set; } = CarryOverType.Forfeit;
    public double CarryOverMaxDays { get; set; }
    public int? CarryOverExpiryMonths { get; set; }

    // ── Cash Conversion ───────────────────────────────────────────────────────
    public bool ConvertToCash { get; set; }
    public decimal CashConversionRate { get; set; } = 1.0m;
    public double? MaxCashConversionDays { get; set; }

    // ── Statutory ─────────────────────────────────────────────────────────────
    public bool IsStatutory { get; set; }
}

public class LeaveApplication : BaseEntity
{
    public Guid LeaveId { get; set; }
    public required virtual Leave Leave { get; set; }
    public Guid EmployeeId { get; set; }
    public DurationType DurationType { get; set; } = DurationType.SingleDay;
    public DateOnly LeaveDateFrom { get; set; }
    public DateOnly LeaveDateTo { get; set; }
    public DayFraction DayFraction { get; set; } = DayFraction.FullDay;
    public PayType PayType { get; set; } = PayType.WithPay;
    // Only meaningful when PayType == WithPay. PerDay (default) pays through the normal
    // DTR/payroll pipeline; OneTime pays a lump sum (GovernmentAmount + CompanyAmount) during
    // the payroll run matching ReleasePayrollDate instead, and the leave's DTR days carry
    // zero PaidLeaveHours (see dtr-api LeavePolicy) — flagged as on-leave but not paid per day.
    public PayoutMode PayoutMode { get; set; } = PayoutMode.PerDay;
    // Government-released and company-funded (variance) portions of a OneTime lump sum.
    // Required together with ReleasePayrollDate when PayoutMode == OneTime.
    public decimal? GovernmentAmount { get; set; }
    public decimal? CompanyAmount { get; set; }
    // Which payroll run releases the OneTime lump sum — matched the same way
    // SalaryAdjustment.PayrollDate is matched against a run's [FromDate, ToDate].
    public DateOnly? ReleasePayrollDate { get; set; }
    // Overrides Leave.EmployerAdvancesPayment for this specific filing. Null = inherit the
    // leave type's default. Needed because the real-world disbursement method can differ per
    // case even for the same leave type (e.g. SSS pays the employee directly if they separated
    // before the claim was filed, instead of the employer advancing it as usual) — see
    // PayrollProcessorService.ApplyOneTimeLeavePayoutsToGross.
    public bool? EmployerAdvancesPayment { get; set; }
    // Tracks the employer's SSS/government reimbursement claim for the EmployerAdvancesPayment
    // case only — see PayrollReportService.GetReimbursementListAsync and
    // LeaveApplicationService.UpdateReimbursementStatusAsync.
    public ReimbursementStatus ReimbursementStatus { get; set; } = ReimbursementStatus.NotFiled;
    public DateOnly? ReimbursementFiledDate { get; set; }
    public DateOnly? ReimbursementReceivedDate { get; set; }
    public string? ReimbursementReferenceNo { get; set; }
    public bool IsManualEntry { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public double TotalMinutes { get; set; }
    public ApprovalStatus ApprovalStatus { get; set; }
    public int? ReviewedBy { get; set; }
    public DateTime? ReviewedOn { get; set; }
    public string? ApplicationRemarks { get; set; }
    public string? SupportingDocumentUrl { get; set; }
    public int? AuditTrailId { get; set; }
}

public class LeaveCredits : BaseEntity
{
    // ── Period ────────────────────────────────────────────────────────────────
    public int PeriodYear { get; set; }         // e.g. 2025
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }

    // ── Ownership ─────────────────────────────────────────────────────────────
    public Guid EmployeeId { get; set; }
    public virtual Employee Employee { get; set; } = null!;
    public Guid LeaveId { get; set; }
    public virtual Leave Leave { get; set; } = null!;

    // ── Balance ───────────────────────────────────────────────────────────────
    public decimal Granted { get; set; }        // total days credited this period (grants + accruals + carry-overs)
    public decimal Used { get; set; }           // total days consumed by DTR-confirmed deductions
    public decimal Balance { get; set; }        // Granted − Used (authoritative hard balance)
    public decimal Reserved { get; set; }       // Phase 1 soft hold: approved-but-not-yet-DTR-posted days
    public decimal AvailableToFile => Balance - Reserved; // what the employee can actually file against
}

public class LeaveLedger : BaseEntity
{
    // ── Ownership ─────────────────────────────────────────────────────────────
    public Guid EmployeeId { get; set; }
    public virtual Employee Employee { get; set; } = null!;
    public Guid LeaveId { get; set; }
    public virtual Leave Leave { get; set; } = null!;
    public Guid LeaveCreditsId { get; set; }
    public virtual LeaveCredits LeaveCredits { get; set; } = null!;

    // ── Entry ─────────────────────────────────────────────────────────────────
    public LedgerEntryType EntryType { get; set; }
    public DateOnly EntryDate { get; set; }
    public decimal Add { get; set; }            // days credited (Grant, Accrual, CarryOver, Reversal, Adjustment+)
    public decimal Less { get; set; }           // days debited  (Deduction, Expiry, CashConversion, Adjustment-)
    public decimal Balance { get; set; }        // running balance after this entry
    public string Particulars { get; set; } = string.Empty;

    // ── Reference ─────────────────────────────────────────────────────────────
    public Guid? ReferenceApplicationId { get; set; }
    public virtual LeaveApplication? Application { get; set; }
    public string? DtrBatchCode { get; set; }   // set on Phase 2 Deduction/Released entries — links ledger to the DTR batch
}
