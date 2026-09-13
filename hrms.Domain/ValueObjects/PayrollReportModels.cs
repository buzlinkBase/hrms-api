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

// Covers every DeductionType category with an amortization schedule — Loans, Cash Advances,
// Cash Bond, etc. — not just loans; named generically since GetDeductionLedgerAsync queries
// DeductionApplicationDetail rows across all categories, not a specific one.
public class DeductionLedgerModel
{
    public Guid EmployeeId { get; set; }
    public string EmployeeNo { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public Guid DeductionId { get; set; }
    public string DeductionTypeName { get; set; } = string.Empty;
    public string DeductionName { get; set; } = string.Empty;
    public decimal TotalPrincipal { get; set; }
    public decimal InterestRate { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal CurrentBalance { get; set; }
}

// One row per employee's Cash Bond DeductionApplication (DeductionType.Code == "CASHBOND") —
// tracks how much has been withheld toward the target (the application's own TotalPrincipal;
// there is no separate employee-profile target field) and how much is still outstanding. See
// PayrollReportService.GetCashBondReportAsync.
public class CashBondReportModel
{
    public Guid EmployeeId { get; set; }
    public string EmployeeNo { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public Guid DeductionId { get; set; }
    public Guid ApplicationId { get; set; }
    public decimal TargetAmount { get; set; }
    public decimal TotalCollected { get; set; }
    public decimal Remaining { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public ApprovalStatus ApprovalStatus { get; set; }
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

// One employee's raw annual aggregate for Year-End Tax Annualization, sourced ONLY from this
// employer's own posted Regular/ThirteenthMonth/LastPay Payroll rows for the year — deliberately
// NOT netted/clamped yet, so TaxAnnualizationService.ComputeAsync can combine these with any
// PriorEmployerTaxRecord (a job-changer's previous employer's BIR 2316 figures) before applying
// the negative-income floor and bracket lookup. See PayrollReportService.GetAnnualTaxAnnualizationInputsAsync.
public class TaxAnnualizationInputModel
{
    public Guid EmployeeId { get; set; }
    public string EmployeeNo { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public int Year { get; set; }
    // From this employee's most-recent-by-PostingPeriod row for the year — used only to scope
    // a run by TaxAnnualizationRunPayload.PayrollGroupIds and to stamp the resulting
    // YearEndAdjustment Payroll row, not part of the tax computation itself.
    public Guid? PayrollGroupId { get; set; }
    public Guid? AreaId { get; set; }
    public Guid? ClientId { get; set; }
    public SalaryType SalaryType { get; set; }
    // Raw current-employer components — Sum(GrossIncome), Sum(NonTaxableBenefits),
    // Sum(SSSContribution + PhilHealthContribution + PagIbigContribution) across the year's
    // included rows. Consolidated with any PriorEmployerTaxRecord and netted/clamped in
    // TaxAnnualizationService.ComputeAsync — see GetAnnualTaxAnnualizationInputsAsync's doc
    // comment for the full per-row formula these are built from.
    public decimal CurrentGrossIncome { get; set; }
    public decimal CurrentNonTaxableBenefits { get; set; }
    public decimal CurrentStatutoryDeductions { get; set; }
    public decimal CurrentWithholdingTaxYTD { get; set; }
    // Sum(NetPay) / 12 across this employee's included rows — used only to gauge whether a
    // computed collection is "large" relative to this employee's typical take-home pay (see
    // TaxAnnualizationPreviewModel.ExceedsLargeCollectionWarning), same "divide by 12"
    // convention ThirteenthMonthPay already uses elsewhere.
    public decimal CurrentAverageMonthlyNetPay { get; set; }
    // Minimum Wage Earners are excluded from annualization entirely (zero adjustment) — see
    // MinimumWageEarnerResolver.IsMinimumWageEarner. Gated on current-employer wage
    // classification only — outside/prior income never affects MWE status for this job.
    public bool IsMinimumWageEarner { get; set; }
    // Mirrors MonthlyRemittanceReturnEmployeeModel.IsUnclassified — MWE status couldn't be
    // resolved (missing Branch/Region/MinimumWageRate data), defaulted to non-MWE. Flagged so
    // HR can fix the employee's Branch/Region setup before relying on the computed adjustment.
    public bool IsUnclassified { get; set; }
}

// The Year-End Tax Annualization preview/review-screen row — the fully consolidated
// (current + prior employer), netted, floored, rounded figures plus the annual bracket lookup
// and the resulting adjustment. See TaxAnnualizationService.ComputeAsync.
public class TaxAnnualizationPreviewModel : TaxAnnualizationInputModel
{
    // True when a PriorEmployerTaxRecord with HasPriorEmployer=true exists for this
    // employee/year — surfaced so HR can confirm the BIR 2316 figures were actually picked up.
    public bool HasPriorEmployerData { get; set; }
    public decimal PriorEmployerGrossIncome { get; set; }
    public decimal PriorEmployerTaxWithheld { get; set; }
    // Current + prior employer gross — informational only, not used in the taxable-income
    // formula itself.
    public decimal AnnualGrossIncome { get; set; }
    // (CurrentGrossIncome - CurrentNonTaxableBenefits - CurrentStatutoryDeductions) +
    // (PriorEmployerGrossIncome - prior non-taxable - prior statutory deductions), floored at 0
    // and rounded to 2dp (MidpointRounding.AwayFromZero) — see ComputeAsync.
    public decimal AnnualTaxableIncome { get; set; }
    // CurrentWithholdingTaxYTD + PriorEmployerTaxWithheld.
    public decimal AnnualWithholdingTaxYTD { get; set; }
    // AnnualTaxCalculator.GetAnnualTaxDue(brackets, AnnualTaxableIncome), rounded to 2dp — 0 for MWEs.
    public decimal AnnualTaxDue { get; set; }
    // AnnualTaxDue - AnnualWithholdingTaxYTD, rounded to 2dp. Positive = additional tax to
    // collect (employee under-withheld this year); negative = refund (employee over-withheld).
    // Always 0 for MWEs.
    public decimal AdjustmentAmount { get; set; }
    public bool IsRefund { get; set; }
    // AdjustmentAmount is a collection (not a refund) larger than
    // CurrentAverageMonthlyNetPay * PayrollSettingsIdentity.KeyLargeTaxCollectionWarningMultiplier
    // — informational only, never blocks Generate. See TaxAnnualizationService.ComputeAsync.
    public bool ExceedsLargeCollectionWarning { get; set; }
    // True when this employee already has a PayrollType.YearEndAdjustment row for Year —
    // shown for HR transparency in Preview, excluded (not silently skipped) from Generate.
    public bool AlreadyGenerated { get; set; }
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
