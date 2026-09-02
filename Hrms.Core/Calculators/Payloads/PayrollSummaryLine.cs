
namespace Hrms.Core.Calculators.Payloads;

public class PayrollSummaryLine
{
    public DateOnly PayPeriodStart { get; set; }
    public DateOnly PayPeriodEnd { get; set; }
    public DateOnly PayrollDate { get; set; }
    // Which calendar month this line's SSS/PhilHealth/Pag-IBIG withholding is credited
    // against, per CrossMonthStatutoryCreditPolicy — see StatutoryCreditDateResolver.
    public DateOnly StatutoryCreditDate { get; set; }
    // The BIR reporting period this payroll posts its withholding tax against, per
    // WTaxCrossMonthCreditPolicy — independently configurable from the statutory credit
    // date above, since tax is conventionally reported against the payout month rather
    // than the period earned. Persisted onto Payroll so WTax reports can filter/group by
    // it directly instead of joining through the WTaxContribution ledger.
    public DateOnly PostingPeriod { get; set; }
    // Raw admin-entered Pay/Release Date from the payroll run request — see Payroll.PayDate.
    public DateOnly? PayDate { get; set; }
    // See Payroll.PayrollBatchId.
    public Guid PayrollBatchId { get; set; }
    // See Payroll.PayrollType.
    public PayrollType PayrollType { get; set; } = PayrollType.Regular;
    // Free-text identity for the whole run — see Payroll.Remarks.
    public string? Remarks { get; set; }
    public Guid EmployeeId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string PayrollPeriod { get; set; } = string.Empty;
    public SalaryType SalaryType { get; set; }
    public decimal DailyRate { get; set; }

    // Earnings
    public decimal BasicPay { get; set; }
    public decimal OvertimePay { get; set; }
    public decimal NightDifferentialPay { get; set; }
    public decimal NightDifferentialOTPay { get; set; }
    public decimal OTPremiumPay { get; set; }
    public decimal NDPremiumPay { get; set; }
    // Combined base (no OT/ND) subtotal across ALL holiday-type days — Legal, Special,
    // RestLegal, RestSpecial, DoubleLegal, RestDoubleLegal — worked AND unworked portions
    // merged together (mirrors DTRPayModel.Holiday exactly). This is a rollup, not an
    // "unworked only" figure — see LegalHolidayUnworkedPay for that.
    public decimal HolidayPay { get; set; }
    // The unworked/no-work portion of Legal Holiday pay only (DTRPayModel.LegalUnWorked,
    // summed for the period) — the one holiday category where "unworked" is a meaningful,
    // separately-tracked figure (Special/RestLegal/RestSpecial pay nothing when unworked
    // per DOLE's "no work, no pay" rule for non-legal holidays).
    public decimal LegalHolidayUnworkedPay { get; set; }

    public decimal RegularDayPay { get; set; }
    public decimal RegularOTPay { get; set; }
    public decimal RegularNDPay { get; set; }
    public decimal RegularNDOTPay { get; set; }

    public decimal RestDayPay { get; set; }
    public decimal RestDayOTPay { get; set; }
    public decimal RestDayNDPay { get; set; }
    public decimal RestDayNDOTPay { get; set; }

    public decimal LegalPay { get; set; }
    public decimal LegalOTPay { get; set; }
    public decimal LegalNDPay { get; set; }
    public decimal LegalNDOTPay { get; set; }

    public decimal SpecialPay { get; set; }
    public decimal SpecialOTPay { get; set; }
    public decimal SpecialNDPay { get; set; }
    public decimal SpecialNDOTPay { get; set; }

    public decimal RestLegalPay { get; set; }
    public decimal RestLegalOTPay { get; set; }
    public decimal RestLegalNDPay { get; set; }
    public decimal RestLegalNDOTPay { get; set; }

    public decimal RestSpecialPay { get; set; }
    public decimal RestSpecialOTPay { get; set; }
    public decimal RestSpecialNDPay { get; set; }
    public decimal RestSpecialNDOTPay { get; set; }

    public decimal DoubleLegalPay { get; set; }
    public decimal DoubleLegalOTPay { get; set; }
    public decimal DoubleLegalNDPay { get; set; }
    public decimal DoubleLegalNDOTPay { get; set; }

    public decimal RestDoubleLegalPay { get; set; }
    public decimal RestDoubleLegalOTPay { get; set; }
    public decimal RestDoubleLegalNDPay { get; set; }
    public decimal RestDoubleLegalNDOTPay { get; set; }

    public List<ProratedAllowanceModel> RegularAllowanceProrated { get; set; } = new(); //For SSS
    public decimal Cola { get; set; }
    public decimal TotalRegularAllowances { get; set; }
    public decimal TotalBonuses { get; set; }
    public decimal Reimbursement { get; set; }
    public decimal TotalOtherIncome { get; set; }
    public decimal TotalDeminimises { get; set; }
    public decimal TotalCommissions { get; set; }
    public decimal TotalAllIncome  { get; set; }
    public decimal GrossIncome { get; set; }

    // Statutory Deductions (Philippines)
    public decimal WithholdingTax { get; set; }    // BIR tax
    public decimal SSSContribution { get; set; }
    public decimal PhilHealthContribution { get; set; }
    public decimal PagIbigContribution { get; set; }
    public decimal OtherDeductions { get; set; }   // Loans, union dues, etc.
    public decimal TotalDeductions { get; set; }
    // Subset of OtherDeductions classified as a loan — see Payroll.TotalLoans.
    public decimal TotalLoans { get; set; }
    //leaves
    public decimal UnpaidLeaves { get; set; }
    public decimal PaidLeaves { get; set; }
    // The slice of PaidLeaves above funded by Government/Shared/Other (not Company) —
    // computed as a proportional share of PaidLeaves by DTR LeavesInfo hours per
    // Leave.PaySource (see PayrollProcessorService.ComputeNonCompanyPaidLeaves), never
    // exceeds PaidLeaves. Display/reporting only — does NOT feed GrossIncome/BasicPay;
    // FIXED's Company-funded paid leave is already covered by its flat monthly rate and
    // VARIABLE's regular pay already folds in paid-leave hours via the DTR "Regular"
    // columns, so this figure is purely informational, not an additional amount to add.
    public decimal NonCompanyPaidLeaves { get; set; }
    // OneTime leave payout (see LeaveApplication.PayoutMode) released this run.
    // GovernmentFundedLeavePay is deliberately excluded from GrossIncome/statutory bases
    // (a government benefit pass-through, not compensation); CompanyFundedLeavePay is
    // included, so it is taxed and factored into SSS/PHIC/HDMF like regular compensation.
    public decimal GovernmentFundedLeavePay { get; set; }
    public decimal CompanyFundedLeavePay { get; set; }
    // Human-readable "which one-time payout(s) made up the two totals above" — e.g.
    // "Vacation Leave: 5,000.00 (Company) + 2,000.00 (Government)". Built in
    // ApplyOneTimeLeavePayoutsToGross from LeaveApplication.Leave.Description (see
    // LeaveApplicationService.LoadOneTimePayoutsAsync's Include). Null when there's no
    // OneTime payout this run.
    public string? OneTimePayoutBreakdown { get; set; }
    // Human-readable "which leave type(s) made up PaidLeaves/UnpaidLeaves this run" — e.g.
    // "Vacation Leave: 8.0h (Paid); Sick Leave: 4.0h (Unpaid)" — sourced from the DTR
    // posting pipeline's own per-day LeavesInfo (DailyRecord.LeavesInfo), NOT from
    // OneTimeLeavePayouts, which is a separate leave-balance cash-out unrelated to any DTR
    // day. See DailyRecordService.LoadLeaveInfoForPayrollRunAsync. Null when no leave was
    // taken this run.
    public string? PaidLeaveBreakdown { get; set; }
    public decimal AbsencesAmount { get; set; }
    public decimal LateAmount { get; set; }
    public decimal UnderTimeAmount { get; set; }

    // Net Pay
    public decimal NetPay { get; set; }

    // Employer Contributions (Philippines)
    public decimal EmployerSSSContribution { get; set; }
    public decimal EmployerPhilHealthContribution { get; set; }
    public decimal EmployerPagIbigContribution { get; set; }
    public decimal EmployerECContribution { get; set; }


    // Optional Reporting Fields
    public Guid? PayrollGroupId { get; set; }
    public Guid? AreaId { get; set; }
    public Guid? ClientId { get; set; }

    public decimal NonTaxableBenefits { get; set; }
    public decimal TaxableBenefits { get; set; }


    public decimal TotalHoursWorked { get; set; }
    public decimal TotalDaysWorked { get; set; }
    public decimal LateMinutes { get; set; }
    public decimal UnderTimeMinutes { get; set; }
    public decimal AbsentDays { get; set; }
    // Sum of every *OTHours category below (pure OT, not the ND-OT combo hours) — the
    // Payroll Summary "OT Total Hr" figure. See ComputeHoursBreakdown.
    public decimal OvertimeHours { get; set; }

    // Per-category DTR hours for this run — a straight per-employee sum of DailyRecord's
    // own hour fields (RegularNetHours, RegularOTHours, ...) across every day in the
    // period, carried forward unchanged by ComputeHoursBreakdown so Payroll Summary's Hours
    // Breakdown tab has the same granularity as the DTR Detail table instead of being
    // dropped once DTR rows are rolled up into a payroll run. Property names match
    // DailyRecord/DtrDetailResponse exactly for consistency across the app, and match
    // Payroll's mirrored fields exactly for Mapster convention-mapping.
    public decimal RegularNetHours { get; set; }
    public decimal RegularOTHours { get; set; }
    public decimal RegularNDHours { get; set; }
    public decimal RegularNDOTHours { get; set; }

    public decimal RestDayHours { get; set; }
    public decimal RestDayOTHours { get; set; }
    public decimal RestDayNDHours { get; set; }
    public decimal RestDayNDOTHours { get; set; }

    public decimal LegalHolHours { get; set; }
    public decimal LegalHolOTHours { get; set; }
    public decimal LegalHolNightDiffHours { get; set; }
    public decimal LegalHolNightDiffOTHours { get; set; }

    public decimal SpecialHolHours { get; set; }
    public decimal SpecialHolOTHours { get; set; }
    public decimal SpecialHolNightDiffHours { get; set; }
    public decimal SpecialHolNightDiffOTHours { get; set; }

    public decimal RestLegalDayHours { get; set; }
    public decimal RestLegalDayOTHours { get; set; }
    public decimal RestLegalDayNDHours { get; set; }
    public decimal RestLegalDayNDOTHours { get; set; }

    public decimal RestSpecialDayHours { get; set; }
    public decimal RestSpecialDayOTHours { get; set; }
    public decimal RestSpecialDayNDHours { get; set; }
    public decimal RestSpecialDayNDOTHours { get; set; }

    public decimal DoubleLegalHours { get; set; }
    public decimal DoubleLegalOTHours { get; set; }
    public decimal DoubleLegalNDHours { get; set; }
    public decimal DoubleLegalNDOTHours { get; set; }

    public decimal RestDoubleLegalHours { get; set; }
    public decimal RestDoubleLegalOTHours { get; set; }
    public decimal RestDoubleLegalNDHours { get; set; }
    public decimal RestDoubleLegalNDOTHours { get; set; }

    public decimal OBHours { get; set; }
    public decimal PaidLeaveHours { get; set; }
    public decimal UnpaidLeaveHours { get; set; }

    public decimal TaxableIncome { get; set; }
    //(Gross less non-taxable allowances/contributions before BIR tax lookup)
    public decimal NonTaxableIncome { get; set; }
    public decimal SSSMandatoryProvidentFund { get; set; }
    //(SSS WISP contribution -employee share)
    public decimal EmployerSSSMandatoryProvidentFund { get; set; }
    //(SSS WISP contribution -employer share)
    public decimal PagIbig2Contribution { get; set; }

    public DateTime ProcessedAt { get; set; }
    public string? ProcessedBy { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public string PositionName { get; set; } = string.Empty;
    public string EmployeeCode { get; set; } = string.Empty;

    public List<DTRPayModel> TimeHourPayResults { get; set; } = new();
    public List<OtherIncomeInfo> OtherIncomeCollection { get; set; } = new();
    public List<DeductionInfo> DeductionCollection { get; set; } = new();
}
