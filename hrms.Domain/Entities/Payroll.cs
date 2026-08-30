namespace Hrms.Domain.Entities;

public class Payroll : BaseEntity, IPostedFilter, IDateFilter
{
    public string BatchCode { get; set; } = string.Empty;
    public DateOnly PayPeriodStart { get; set; }
    public DateOnly PayPeriodEnd { get; set; }
    public DateOnly PayrollDate { get; set; }
    // The BIR reporting period this record's withholding tax posts against, per
    // Company Policy > WTaxCrossMonthCreditPolicy (defaults to the payout month; can
    // instead follow the period-earned month, same as SSS/PhilHealth/Pag-IBIG, per client
    // preference). Use this for WTax remittance reports rather than PayrollDate/ToDate.
    public DateOnly PostingPeriod { get; set; }
    // The PayrollBatch (header/master row for this Generate run) this row belongs to — a
    // real FK, not just a shared Guid, so the whole run's Payroll rows can be looked up or
    // deleted by it directly. See PayrollBatch for the batch-level facts (period, pay date,
    // remarks, posted status) that used to be duplicated per row here.
    public Guid PayrollBatchId { get; set; }
    // Denormalized copies of PayrollBatch.PayDate/Remarks/IsPosted, kept in sync by
    // PayrollProcessorService — cheap to read per row (payslip header, Payroll Summary
    // grid) without joining PayrollBatch. PayrollBatch is the canonical source for
    // Post/Delete decisions. Raw admin-entered Pay/Release Date from the payroll run
    // request, kept for audit even though PostingPeriod already holds the resolved credit
    // date — null unless the WTaxCrossMonthCreditPolicy/CrossMonthStatutoryCreditPolicy
    // PayDate option was used.
    public DateOnly? PayDate { get; set; }
    public string? Remarks { get; set; }
    public Guid EmployeeId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string PayrollPeriod { get; set; } = string.Empty;
    // Snapshot of the employee's rate/salary type at the time this payroll ran — see
    // PayrollProcessorService.InitializePayrollLine. Not sourced from the live Employee
    // record so a later rate change doesn't retroactively change a past payslip.
    public decimal DailyRate { get; set; }
    public SalaryType SalaryType { get; set; }
    // Sum of scheduled deductions classified as a loan (DeductionType.Code containing
    // "LOAN") — a subset of OtherDeductions, kept separately so a payslip can show Loans
    // as its own line without changing what OtherDeductions means to existing consumers.
    public decimal TotalLoans { get; set; }

    // Earnings
    public decimal OTPremiumPay { get; set; }
    public decimal NDPremiumPay { get; set; }

    public decimal OvertimePay { get; set; }
    // Renamed to match PayrollSummaryLine.NightDifferentialPay/NightDifferentialOTPay exactly
    // — Mapster's convention mapping only maps same-named members, so the old NDPay/NDOTPay
    // names silently never received a value from a generated payroll run.
    public decimal NightDifferentialPay { get; set; }
    public decimal NightDifferentialOTPay { get; set; }

    // Renamed from BasicSalary to match PayrollSummaryLine.BasicPay — same reason as above.
    public decimal BasicPay { get; set; }
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
    // Combined base (no OT/ND) subtotal across ALL holiday-type days — Legal, Special,
    // RestLegal, RestSpecial, DoubleLegal, RestDoubleLegal — worked AND unworked merged
    // together. A rollup, not an "unworked only" figure — see LegalHolidayUnworkedPay.
    public decimal HolidayPay { get; set; }
    // The unworked/no-work portion of Legal Holiday pay only — the one holiday category
    // where "unworked" is meaningfully separate (Special/RestLegal/RestSpecial pay nothing
    // when unworked, per DOLE's "no work, no pay" rule for non-legal holidays).
    public decimal LegalHolidayUnworkedPay { get; set; }
    //public virtual List<ProratedAllowanceForSSS> RegularAllowanceProrated { get; set; } = new(); //For SSS
    public decimal Cola { get; set; }
    public decimal TotalRegularAllowances { get; set; }
    public decimal TotalBonuses { get; set; }
    public decimal Reimbursement { get; set; }
    public decimal TotalOtherIncome { get; set; }
    public decimal TotalDeminimises { get; set; }
    public decimal TotalCommissions { get; set; }
    public decimal GrossIncome { get; set; }

    // Statutory Deductions (Philippines)
    public decimal WithholdingTax { get; set; }    // BIR tax
    public decimal SSSContribution { get; set; }
    public decimal PhilHealthContribution { get; set; }
    public decimal PagIbigContribution { get; set; }
    public decimal OtherDeductions { get; set; }   // Loans, union dues, etc.
    public decimal TotalDeductions { get; set; }
    //leaves
    public decimal UnpaidLeaves { get; set; }
    public decimal PaidLeaves { get; set; }
    // OneTime leave payout (see LeaveApplication.PayoutMode) released this run.
    // GovernmentFundedLeavePay is deliberately excluded from GrossIncome/statutory bases
    // (a government benefit pass-through, not compensation); CompanyFundedLeavePay is
    // included, so it is taxed and factored into SSS/PHIC/HDMF like regular compensation.
    public decimal GovernmentFundedLeavePay { get; set; }
    public decimal CompanyFundedLeavePay { get; set; }
    public decimal AbsencesAmount { get; set; }
    public decimal LateAmount { get; set; }
    public decimal UnderTimeAmount { get; set; }

    // Net Pay
    public decimal GrossPay { get; set; }
    public decimal NetPay { get; set; }

    // Employer Contributions (Philippines)
    public decimal EmployerSSSContribution { get; set; }
    public decimal EmployerPhilHealthContribution { get; set; }
    public decimal EmployerPagIbigContribution { get; set; }
    public decimal EmployerECContribution { get; set; }

    public Guid? PayrollGroupId { get; set; }
    public Guid? AreaId { get; set; }
    public Guid? ClientId { get; set; }

    public decimal NonTaxableBenefits { get; set; }
    public decimal TaxableBenefits { get; set; }
    //public List<DTRPayModel> TimeHourPayResults { get; set; } = new();
    //public List<OtherIncomeSchedules> OtherIncomeCollection { get; set; } = new();
    //public List<DeductionInfo> DeductionCollection { get; set; } = new();
    public bool IsPosted { get; set; }
}
