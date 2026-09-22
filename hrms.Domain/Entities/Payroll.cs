using BuzlinkRepository;

namespace Hrms.Domain.Entities;

[DisableSoftDelete]
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

    // Period-level (this whole payroll line, not just one day) segregation of the already-blended
    // {Category}OTPay/NDPay/NDOTPay above, each figure priced against ONE rate alone (never
    // compounded with another tier):
    // - OTBasePay: hours(OT) * rawOTRate * hourlyRate -- the raw HOLIDAY_OT/OVERTIME rate alone,
    //   ignoring day-type compounding and any client override on the actual paid OTPay.
    // - NDBasePay: hours(ND) * dayTypeRate * hourlyRate -- the day-type rate alone, no ND premium.
    // - NDOTBasePay: hours(NDOT) * rawOTRate * hourlyRate -- same raw OT rate as OTBasePay above,
    //   applied to the NDOT hours, ignoring the day-type rate AND the night-diff premium.
    // - NDPremiumPay / NDOTPremiumPay: hours * (nightDiffRate - 1) * hourlyRate -- e.g. a 1.10
    //   rate contributes only the .10, against the plain hourly rate, not any other tier.
    // Additive recording for extraction/reporting, not a decomposition that sums back to the
    // blended totals above -- matches the client's own spreadsheet, which shows the same four
    // figures this way. Mirrors PayrollDtrDetail/DTRPayModel's identical per-day fields, summed
    // across the period by EmployeePayrollLineService.ComputeBasicSalary.
    public decimal RegularNDBasePay { get; set; }
    public decimal RegularNDPremiumPay { get; set; }
    public decimal RegularNDOTBasePay { get; set; }
    public decimal RegularOTBasePay { get; set; }
    public decimal RegularNDOTPremiumPay { get; set; }

    public decimal RestDayNDBasePay { get; set; }
    public decimal RestDayNDPremiumPay { get; set; }
    public decimal RestDayNDOTBasePay { get; set; }
    public decimal RestDayOTBasePay { get; set; }
    public decimal RestDayNDOTPremiumPay { get; set; }

    public decimal LegalNDBasePay { get; set; }
    public decimal LegalNDPremiumPay { get; set; }
    public decimal LegalNDOTBasePay { get; set; }
    public decimal LegalOTBasePay { get; set; }
    public decimal LegalNDOTPremiumPay { get; set; }

    public decimal SpecialNDBasePay { get; set; }
    public decimal SpecialNDPremiumPay { get; set; }
    public decimal SpecialNDOTBasePay { get; set; }
    public decimal SpecialOTBasePay { get; set; }
    public decimal SpecialNDOTPremiumPay { get; set; }

    public decimal RestLegalNDBasePay { get; set; }
    public decimal RestLegalNDPremiumPay { get; set; }
    public decimal RestLegalNDOTBasePay { get; set; }
    public decimal RestLegalOTBasePay { get; set; }
    public decimal RestLegalNDOTPremiumPay { get; set; }

    public decimal RestSpecialNDBasePay { get; set; }
    public decimal RestSpecialNDPremiumPay { get; set; }
    public decimal RestSpecialNDOTBasePay { get; set; }
    public decimal RestSpecialOTBasePay { get; set; }
    public decimal RestSpecialNDOTPremiumPay { get; set; }

    public decimal DoubleLegalNDBasePay { get; set; }
    public decimal DoubleLegalNDPremiumPay { get; set; }
    public decimal DoubleLegalNDOTBasePay { get; set; }
    public decimal DoubleLegalOTBasePay { get; set; }
    public decimal DoubleLegalNDOTPremiumPay { get; set; }

    public decimal RestDoubleLegalNDBasePay { get; set; }
    public decimal RestDoubleLegalNDPremiumPay { get; set; }
    public decimal RestDoubleLegalNDOTBasePay { get; set; }
    public decimal RestDoubleLegalOTBasePay { get; set; }
    public decimal RestDoubleLegalNDOTPremiumPay { get; set; }

    // Combined base (no OT/ND) subtotal across ALL holiday-type days — Legal, Special,
    // RestLegal, RestSpecial, DoubleLegal, RestDoubleLegal — worked AND unworked merged
    // together. A rollup, not an "unworked only" figure — see LegalHolidayUnworkedPay.
    public decimal HolidayPay { get; set; }
    // The unworked/no-work portion of Legal Holiday pay — Special/RestSpecial pay nothing when
    // unworked (DOLE's "no work, no pay" rule for non-legal holidays), so only the four
    // legal-holiday-involving categories (Legal, RestLegal, DoubleLegal, RestDoubleLegal) ever
    // have an unworked component. Legal's is 1.0x; RestLegal's below is also 1.0x; DoubleLegal/
    // RestDoubleLegal pay 2.0x unworked (a second legal holiday still requires full pay even if
    // unworked). Each is a subset of its category's blended {Category}Pay total above, not
    // additive — see BasicPayrollModel.Calculate/DayTypePayCalculator.
    public decimal LegalHolidayUnworkedPay { get; set; }
    public decimal RestLegalUnworkedPay { get; set; }
    public decimal DoubleLegalUnworkedPay { get; set; }
    public decimal RestDoubleLegalUnworkedPay { get; set; }
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
    // Mirrors PayrollSummaryLine.NonCompanyPaidLeaves — see its doc comment. Display only.
    public decimal NonCompanyPaidLeaves { get; set; }
    // OneTime leave payout (see LeaveApplication.PayoutMode) released this run.
    // GovernmentFundedLeavePay is deliberately excluded from GrossIncome/statutory bases
    // (a government benefit pass-through, not compensation); CompanyFundedLeavePay is
    // included, so it is taxed and factored into SSS/PHIC/HDMF like regular compensation.
    public decimal GovernmentFundedLeavePay { get; set; }
    public decimal CompanyFundedLeavePay { get; set; }
    // Persisted copies of PayrollSummaryLine.OneTimePayoutBreakdown/PaidLeaveBreakdown —
    // property names must match exactly for Mapster's convention mapping. Rows generated
    // before these fields existed will read null.
    public string? OneTimePayoutBreakdown { get; set; }
    public string? PaidLeaveBreakdown { get; set; }
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
    // Persisted copies of PayrollSummaryLine.TaxableIncome/NonTaxableIncome (gross less
    // non-taxable allowances/contributions before the BIR tax lookup) — needed for BIR
    // Alphalist/2316 annual rollups. Property names must match PayrollSummaryLine exactly:
    // Mapster's convention mapping (used by PayrollProcessorService.GenerateAsync) only maps
    // identically-named members. Rows generated before this field existed will read 0.
    public decimal TaxableIncome { get; set; }
    public decimal NonTaxableIncome { get; set; }
    // Persisted per-day DTR breakdown (target of PayrollSummaryLine.TimeHourPayResults via
    // Mapster's convention mapping — same property name, DTRPayModel -> PayrollDtrDetail
    // element mapping configured explicitly in MappingProfile.cs). Replaces the DTRPayModel
    // list that used to be calculation-only and never survived a save — now each day's
    // DtrId/ClientId/DepartmentId/PayrollGroupId (and full pay/hour breakdown) round-trips
    // through Generate/Save for later billing generation and other reporting that needs
    // DTR-to-payroll traceability. See PayrollDtrDetail below.
    public virtual List<PayrollDtrDetail> TimeHourPayResults { get; set; } = new();
    //public List<OtherIncomeSchedules> OtherIncomeCollection { get; set; } = new();
    // Persisted record of exactly which DeductionApplicationDetail installments (loans and
    // other scheduled deductions) this Payroll actually withheld and how much — the saved
    // counterpart of the calculation-only DeductionInfo list PayrollSummaryLine.
    // DeductionCollection carries (same property name, DeductionInfo -> PayrollDeductionDetail
    // element mapping configured explicitly in MappingProfile.cs, since DeductionInfo.Id means
    // "the DeductionApplicationDetail this came from," not this entity's own Id). Written at
    // Generate time like TimeHourPayResults above, but only READ back at Post time — see
    // PayrollService.PostBatchAsync, which is what actually reduces DeductionApplicationDetail.
    // Balance by these amounts. A draft that's regenerated or deleted before posting never
    // touches Balance.
    public virtual List<PayrollDeductionDetail> DeductionCollection { get; set; } = new();
    public bool IsPosted { get; set; }
    // Setup > Payslip/13th Month/Last Pay > Received by Employee — set once the employee
    // confirms receipt of this row's document in the Employee Portal (MeController.
    // AcknowledgeMyPayslip). Null means not yet acknowledged; covers all three document types
    // uniformly since Payslip/13th Month/Last Pay are all just Payroll rows differentiated by
    // PayrollType, rendered by the same PayslipDocument class. Informational only — nothing
    // else in the system reads or gates on this.
    public DateTime? AcknowledgedAt { get; set; }
    // Denormalized copy of PayrollBatch.PayrollType, kept in sync by PayrollProcessorService
    // — same pattern as IsPosted — so report queries (e.g. next year's 13th month/Alphalist
    // calculation) can exclude a 13th month payout row without joining PayrollBatch.
    public PayrollType PayrollType { get; set; } = PayrollType.Regular;
    // Setup > Company Policy > OT/ND Calculation Method, recorded as it was at the moment this
    // row was generated (not read live) — a later company-wide setting change must never
    // retroactively reinterpret an already-paid payroll row. PayslipHoursDocument.BuildRows
    // reads this to know whether {Category}OTBasePay/NDOTBasePay hold the full OT rate
    // (Compounded) or just the OT premium delta (Additive) for this specific row.
    public OtNdCalculationMethod OtNdCalculationMethod { get; set; } = OtNdCalculationMethod.Compounded;

    // Persisted copies of PayrollSummaryLine's per-category DTR hours (see that class's
    // ComputeHoursBreakdown doc comment) — needed so the Payroll Summary page's Hours
    // Breakdown tab has real figures once a run is saved and re-fetched via GET /payrolls,
    // not just on the live Calculate/Generate preview. Property names must match
    // PayrollSummaryLine exactly: Mapster's convention mapping (used by
    // PayrollProcessorService.GenerateAsync) only maps identically-named members. Rows
    // generated before this field existed will read 0.
    public decimal OvertimeHours { get; set; }
    public decimal RegularNetHours { get; set; }
    public decimal RegularOTHours { get; set; }
    public decimal RegularNDHours { get; set; }
    public decimal RegularNDOTHours { get; set; }

    // Setup > Client > Settings > Allowances > Retirement (days/year) -- this run's computed
    // accrual, informational only (excluded from GrossIncome/NetPay above). Only read back at
    // Post time to grow RetirementFund.Balance -- see PayrollService.ProcessRetirementFundActivityAsync.
    public decimal RetirementAccrual { get; set; }
    // Last Pay > IncludeRetirementPayout -- the employee's RetirementFund.Balance cashed out at
    // separation (LastPayrollService.GenerateAsync), non-taxable so it's added straight to
    // NetPay rather than GrossIncome, same treatment as an employer-advanced government leave
    // payout. Only read back at Post time to debit RetirementFund.Balance -- see
    // PayrollService.ProcessRetirementFundActivityAsync.
    public decimal RetirementPayout { get; set; }

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
}

// Persisted per-day DTR breakdown for one Payroll line — the saved counterpart of the
// calculation-only DTRPayModel (Hrms.Core/Calculators/Payloads/DTRPayModel.cs), which this
// mirrors field-for-field. Child table pattern (PayrollId FK, no back-nav) — same convention
// as DeductionApplication/DeductionApplicationDetail, not a JSON column. See Payroll.
// TimeHourPayResults and MappingProfile's DTRPayModel -> PayrollDtrDetail config.
[DisableSoftDelete]
public class PayrollDtrDetail : BaseEntity
{
    public Guid PayrollId { get; set; }
    public Guid? DtrId { get; set; }
    public string? DTRRef { get; set; }
    public DateOnly Date { get; set; }
    public Guid EmployeeId { get; set; }
    // That specific day's own DailyRecord.ClientId/DepartmentId/PayrollGroupId (set at
    // DTR-generation time from that day's attendance) — not the employee's current/master
    // settings. See DTRPayModel's identical fields for why.
    public Guid? ClientId { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? PayrollGroupId { get; set; }
    public decimal DailyRate { get; set; }
    public SalaryType SalaryType { get; set; }
    public WorkType WorkType { get; set; }

    public decimal LateAmount { get; set; }
    public decimal UTAmount { get; set; }
    public decimal AbsentAmount { get; set; }
    public decimal PaidLeave { get; set; }
    public decimal UnpaidLeave { get; set; }

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

    public decimal LegalWorked { get; set; }
    public decimal LegalUnWorked { get; set; }
    public decimal RestLegalWorked { get; set; }
    public decimal RestLegalUnWorked { get; set; }
    public decimal DoubleLegalWorked { get; set; }
    public decimal DoubleLegalUnworked { get; set; }
    public decimal RestDoubleLegalWorked { get; set; }
    public decimal RestDoubleLegalUnworked { get; set; }
    public decimal TotalExcludingBasic { get; set; }
    public decimal Holiday { get; set; }
    public decimal NDPremiumPay { get; set; }
    public decimal OTPremiumPay { get; set; }

    public decimal TotalOT { get; set; }
    public decimal TotalND { get; set; }
    public decimal TotalNDOT { get; set; }

    // Per-category segregation of the already-blended {Category}OTPay/NDPay/NDOTPay above, each
    // figure priced against ONE rate alone (never compounded with another tier):
    // - OTBasePay: hours(OT) * rawOTRate * hourlyRate -- the raw HOLIDAY_OT/OVERTIME rate alone,
    //   ignoring day-type compounding and any client override on the actual paid OTPay.
    // - NDBasePay: hours(ND) * dayTypeRate * hourlyRate -- the day-type rate alone, no ND premium.
    // - NDOTBasePay: hours(NDOT) * rawOTRate * hourlyRate -- same raw OT rate as OTBasePay above,
    //   applied to the NDOT hours, ignoring the day-type rate AND the night-diff premium.
    // - NDPremiumPay / NDOTPremiumPay: hours * (nightDiffRate - 1) * hourlyRate -- e.g. a 1.10
    //   rate contributes only the .10, against the plain hourly rate, not any other tier.
    // Additive recording for extraction/reporting, not a decomposition that sums back to the
    // blended totals above -- matches the client's own spreadsheet, which shows the same four
    // figures this way. Mirrors DTRPayModel.
    public decimal RegularNDBasePay { get; set; }
    public decimal RegularNDPremiumPay { get; set; }
    public decimal RegularNDOTBasePay { get; set; }
    public decimal RegularOTBasePay { get; set; }
    public decimal RegularNDOTPremiumPay { get; set; }

    public decimal RestDayNDBasePay { get; set; }
    public decimal RestDayNDPremiumPay { get; set; }
    public decimal RestDayNDOTBasePay { get; set; }
    public decimal RestDayOTBasePay { get; set; }
    public decimal RestDayNDOTPremiumPay { get; set; }

    public decimal LegalNDBasePay { get; set; }
    public decimal LegalNDPremiumPay { get; set; }
    public decimal LegalNDOTBasePay { get; set; }
    public decimal LegalOTBasePay { get; set; }
    public decimal LegalNDOTPremiumPay { get; set; }

    public decimal SpecialNDBasePay { get; set; }
    public decimal SpecialNDPremiumPay { get; set; }
    public decimal SpecialNDOTBasePay { get; set; }
    public decimal SpecialOTBasePay { get; set; }
    public decimal SpecialNDOTPremiumPay { get; set; }

    public decimal RestLegalNDBasePay { get; set; }
    public decimal RestLegalNDPremiumPay { get; set; }
    public decimal RestLegalNDOTBasePay { get; set; }
    public decimal RestLegalOTBasePay { get; set; }
    public decimal RestLegalNDOTPremiumPay { get; set; }

    public decimal RestSpecialNDBasePay { get; set; }
    public decimal RestSpecialNDPremiumPay { get; set; }
    public decimal RestSpecialNDOTBasePay { get; set; }
    public decimal RestSpecialOTBasePay { get; set; }
    public decimal RestSpecialNDOTPremiumPay { get; set; }

    public decimal DoubleLegalNDBasePay { get; set; }
    public decimal DoubleLegalNDPremiumPay { get; set; }
    public decimal DoubleLegalNDOTBasePay { get; set; }
    public decimal DoubleLegalOTBasePay { get; set; }
    public decimal DoubleLegalNDOTPremiumPay { get; set; }

    public decimal RestDoubleLegalNDBasePay { get; set; }
    public decimal RestDoubleLegalNDPremiumPay { get; set; }
    public decimal RestDoubleLegalNDOTBasePay { get; set; }
    public decimal RestDoubleLegalOTBasePay { get; set; }
    public decimal RestDoubleLegalNDOTPremiumPay { get; set; }
}

// Persisted record of one DeductionApplicationDetail installment (loan or other scheduled
// deduction) actually withheld for this Payroll — the saved counterpart of the
// calculation-only DeductionInfo. See Payroll.DeductionCollection and MappingProfile's
// DeductionInfo -> PayrollDeductionDetail config.
[DisableSoftDelete]
public class PayrollDeductionDetail : BaseEntity
{
    public Guid PayrollId { get; set; }
    public Guid DeductionApplicationDetailId { get; set; }
    public Guid DeductionId { get; set; }
    public decimal Amount { get; set; }
}
