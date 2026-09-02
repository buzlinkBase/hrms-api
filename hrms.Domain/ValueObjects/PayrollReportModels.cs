namespace Hrms.Domain.ValueObjects;

// Shared shape for the SSS / PhilHealth / Pag-IBIG / BIR Withholding Tax remittance
// reports — each source ledger (SSSContribution/PHICContribution/HDMFContribution/
// WTaxContribution) has a slightly different EE/ER split, so fields not applicable to
// a given ledger (e.g. WTax has no employer share) are simply left at 0.
public class ContributionRemittanceModel
{
    public Guid EmployeeId { get; set; }
    public string EmployeeNo { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string GovIdNumber { get; set; } = string.Empty;
    public DateOnly PayrollFrom { get; set; }
    public DateOnly PayrollTo { get; set; }
    public DateOnly PayrollDate { get; set; }
    public decimal EmployeeShare { get; set; }
    public decimal EmployerShare { get; set; }
    public decimal TotalContribution { get; set; }
}

public class BankDisbursementModel
{
    public Guid EmployeeId { get; set; }
    public string EmployeeNo { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public PaymentMethod ModeOfPayment { get; set; }
    public string BankName { get; set; } = string.Empty;
    public string BankNo { get; set; } = string.Empty;
    public DateOnly PayPeriodStart { get; set; }
    public DateOnly PayPeriodEnd { get; set; }
    public decimal NetPay { get; set; }
}

public class LoanLedgerModel
{
    public Guid EmployeeId { get; set; }
    public string EmployeeNo { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public Guid DeductionId { get; set; }
    public string LoanTypeName { get; set; } = string.Empty;
    public string LoanName { get; set; } = string.Empty;
    public decimal TotalPrincipal { get; set; }
    public decimal InterestRate { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal CurrentBalance { get; set; }
}

public class CostSummaryModel
{
    public Guid? GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public int EmployeeCount { get; set; }
    public decimal TotalBasicPay { get; set; }
    public decimal TotalGrossIncome { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal TotalNetPay { get; set; }
    public decimal EmployerContributionsCost { get; set; }
}

public class YtdPayrollSummaryModel
{
    public Guid EmployeeId { get; set; }
    public string EmployeeNo { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public int Year { get; set; }
    public decimal TotalBasicPay { get; set; }
    public decimal TotalOvertimePay { get; set; }
    public decimal TotalHolidayPay { get; set; }
    public decimal TotalAllowances { get; set; }
    public decimal TotalOtherIncome { get; set; }
    public decimal TotalGrossIncome { get; set; }
    public decimal TotalSSS { get; set; }
    public decimal TotalPhilHealth { get; set; }
    public decimal TotalPagIbig { get; set; }
    public decimal TotalWithholdingTax { get; set; }
    public decimal TotalOtherDeductions { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal TotalNetPay { get; set; }
}

// Manual HR correction of an employee's leave credits balance — see
// LeaveLedgerService.AdjustBalanceAsync. Recorded as a LedgerEntryType.Adjustment entry,
// not a raw overwrite, so the ledger's audit trail stays intact.
public class AdjustLeaveCreditsPayload
{
    public Guid EmployeeId { get; set; }
    public Guid LeaveId { get; set; }
    public int Year { get; set; }
    public decimal NewBalance { get; set; }
    public string Particulars { get; set; } = string.Empty;
}

public class LeaveCreditsBalanceModel
{
    public Guid EmployeeId { get; set; }
    public string EmployeeNo { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public Guid LeaveId { get; set; }
    public string LeaveCode { get; set; } = string.Empty;
    public string LeaveDescription { get; set; } = string.Empty;
    public int PeriodYear { get; set; }
    public decimal Granted { get; set; }
    public decimal Used { get; set; }
    public decimal Balance { get; set; }
    public decimal Reserved { get; set; }
    public decimal AvailableToFile { get; set; }
}

public class ThirteenthMonthModel
{
    public Guid EmployeeId { get; set; }
    public string EmployeeNo { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public int Year { get; set; }
    public decimal TotalBasicPayForYear { get; set; }
    public decimal ThirteenthMonthPay { get; set; }
    // Sum of Payroll.TotalBonuses (IncomeClassType.SpecialBonus-classified income only) across
    // this employee's regular payroll rows for the year — combined with ThirteenthMonthPay
    // against the exemption ceiling per Payroll Settings' documented rule. See
    // PayrollProcessorService.ComputeRemainingThirteenthMonthCeiling.
    public decimal TotalSpecialBonusesForYear { get; set; }
    // "NotGenerated" (no 13th month Payroll row exists yet) / "Draft" (generated, not yet
    // posted) / "Posted" (released) — for the released/unreleased tracker.
    public string Status { get; set; } = "NotGenerated";
    // Only set once a 13th month payout row exists (Status != "NotGenerated").
    public decimal? NetPay { get; set; }
    // The generated Payroll row's own Id — needed to print its payslip (GET payrolls/{id}/print).
    // Null when Status == "NotGenerated".
    public Guid? PayrollId { get; set; }
}

// BIR Form 1601-C's actual return figures for one posting period — company-wide totals,
// not a per-employee breakdown. Filed through eBIRForms/eFPS (BIR does not accept a raw
// file upload for this return the way SSS/PhilHealth/Pag-IBIG do), so this exists to give
// the preparer the exact numbers to transcribe rather than to be a submittable file itself.
public class MonthlyRemittanceReturnModel
{
    public DateOnly PeriodFrom { get; set; }
    public DateOnly PeriodTo { get; set; }
    public int EmployeeCount { get; set; }
    public decimal TotalTaxableCompensation { get; set; }
    public decimal TotalTaxWithheld { get; set; }
}

// One row per employee per year — the BIR Alphalist's per-employee annual compensation/tax
// breakdown. Also the source data for each employee's 2316 certificate.
public class AlphalistEntryModel
{
    public Guid EmployeeId { get; set; }
    public string EmployeeNo { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string TIN { get; set; } = string.Empty;
    public int Year { get; set; }
    public decimal GrossCompensation { get; set; }
    public decimal NonTaxableCompensation { get; set; }
    public decimal TaxableCompensation { get; set; }
    public decimal ThirteenthMonthPay { get; set; }
    public decimal TotalSSS { get; set; }
    public decimal TotalPhilHealth { get; set; }
    public decimal TotalPagIbig { get; set; }
    public decimal TotalTaxWithheld { get; set; }
}

// Single-employee annual data for a BIR 2316 certificate — AlphalistEntryModel's figures
// plus the employee/employer identification fields the certificate itself needs.
public class Bir2316Model
{
    public Guid EmployeeId { get; set; }
    public string EmployeeNo { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string TIN { get; set; } = string.Empty;
    public string RDOCode { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string CivilStatus { get; set; } = string.Empty;
    public int Year { get; set; }
    public decimal GrossCompensation { get; set; }
    public decimal NonTaxableCompensation { get; set; }
    public decimal TaxableCompensation { get; set; }
    public decimal ThirteenthMonthPay { get; set; }
    public decimal TotalSSS { get; set; }
    public decimal TotalPhilHealth { get; set; }
    public decimal TotalPagIbig { get; set; }
    public decimal TotalTaxWithheld { get; set; }
}
