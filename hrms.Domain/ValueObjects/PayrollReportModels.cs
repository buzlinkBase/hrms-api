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

// One row per OneTime, employer-advanced government leave payout — tracks the employer's
// SSS/government reimbursement claim from filing through to being paid back. See
// PayrollReportService.GetReimbursementListAsync and
// LeaveApplicationService.UpdateReimbursementStatusAsync.
public class ReimbursementListModel
{
    public Guid LeaveApplicationId { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeNo { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string LeaveDescription { get; set; } = string.Empty;
    public DateOnly LeaveDateFrom { get; set; }
    public DateOnly LeaveDateTo { get; set; }
    public DateOnly? ReleasePayrollDate { get; set; }
    public decimal GovernmentAmount { get; set; }
    public ReimbursementStatus Status { get; set; }
    public DateOnly? FiledDate { get; set; }
    public DateOnly? ReceivedDate { get; set; }
    public string? ReferenceNo { get; set; }
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

// BIR Form 1601-C's actual return figures for one posting period, matching the physical
// form's own Line 15/16A/16B/16C/17/18/19 layout — company-wide totals, summed from the
// per-employee MonthlyRemittanceReturnEmployeeModel rows below. Filed through eBIRForms/eFPS
// (BIR does not accept a raw file upload for this return the way SSS/PhilHealth/Pag-IBIG do),
// so this exists to give the preparer the exact numbers to transcribe rather than to be a
// submittable file itself. See PayrollReportService.GetMonthlyRemittanceReturnAsync and
// PayrollReportService.SummarizeMonthlyRemittanceReturn.
public class MonthlyRemittanceReturnModel
{
    public DateOnly PeriodFrom { get; set; }
    public DateOnly PeriodTo { get; set; }
    // A filing declaration, not derived from payroll data — echoed from the caller's request.
    public bool AmendedReturn { get; set; }
    public int EmployeeCount { get; set; }
    public decimal Line15_TotalCompensation { get; set; }
    public decimal Line16A_StatutoryMinimumWage { get; set; }
    public decimal Line16B_MWEPremiumPay { get; set; }
    public decimal Line16C_OtherNonTaxable { get; set; }
    public decimal Line17_TotalNonTaxable { get; set; }
    public decimal Line18_TaxableCompensation { get; set; }
    public decimal Line19_TaxWithheld { get; set; }
    // Spec Validation 3 — Line 18 &gt; 0 but nothing was withheld is a real data-quality signal
    // worth surfacing (unlike Validations 1/2, which are arithmetic identities this summary
    // guarantees by construction — see SummarizeMonthlyRemittanceReturn's own unit tests).
    public bool HasUnwithheldTaxWarning { get; set; }
    // Count of employees whose Minimum-Wage-Earner status could NOT be determined this period
    // (no Branch assigned, Branch has no RegionCode, or no MinimumWageRate exists for that
    // region as of the period) — they're defaulted to non-MWE (ATC KR010, Line 16A/16B = 0)
    // rather than failing the whole report, but that default may be wrong for an employee who
    // actually is a minimum wage earner. See MonthlyRemittanceReturnEmployeeModel.IsUnclassified.
    public int UnclassifiedEmployeeCount { get; set; }
}

// One row per employee for the period — backs both the summary totals above (by summation)
// and the report UI's drill-down (click a line, see the employee rows behind it).
public class MonthlyRemittanceReturnEmployeeModel
{
    public Guid EmployeeId { get; set; }
    public string EmployeeNo { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public bool IsMinimumWageEarner { get; set; }
    // "KR020" (Minimum Wage Earner, tax-exempt) or "KR010" (Compensation Income, graduated
    // table) — derived from IsMinimumWageEarner, not a stored/configurable value; these are
    // the only two ATC codes BIR 1601-C recognizes for compensation income.
    public string AtcCode { get; set; } = string.Empty;
    public decimal GrossCompensation { get; set; }
    public decimal StatutoryMinimumWage { get; set; }
    public decimal MWEPremiumPay { get; set; }
    public decimal OtherNonTaxable { get; set; }
    public decimal TaxableCompensation { get; set; }
    public decimal TaxWithheld { get; set; }
    // True when this employee had a regular payroll row this period but their MWE status
    // couldn't be resolved (missing Branch/Region/MinimumWageRate data) — defaulted to
    // IsMinimumWageEarner=false rather than failing the report. Flag this employee's
    // Branch/Region setup before filing if they're actually a minimum wage earner.
    public bool IsUnclassified { get; set; }
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
