using BuzlinkRepository;

namespace Hrms.Domain.Entities.EmployeeEntities;

// A company's own pre-go-live YTD payroll figures for an employee, for a mid-year system
// cutover — distinct from PriorEmployerTaxRecord, which is a genuinely different employer's
// BIR 2316. Same employer here, just history that lived in a prior system before this one.
// Fields mirror Payroll's own report-relevant columns 1:1 so PayrollReportService's YTD
// aggregations (GetYtdSummaryAsync/GetThirteenthMonthAsync/GetAnnualTaxAnnualizationInputsAsync/
// GetAlphalistAsync/Get2316DataAsync) can fold a row in with a plain field-add.
[DisableSoftDelete]
public class PayrollOpeningBalance : BaseEntity
{
    public Guid EmployeeId { get; set; }
    public virtual Employee Employee { get; set; } = null!;
    public int Year { get; set; }

    // Earnings
    public decimal BasicPay { get; set; }
    public decimal OvertimePay { get; set; }
    public decimal HolidayPay { get; set; }
    public decimal Allowances { get; set; }
    public decimal OtherIncome { get; set; }
    public decimal Bonuses { get; set; }
    // Computed server-side (PayrollOpeningBalanceService.AddAsync/UpdateAsync) from the
    // components above — never trusted from the client — so it can never drift from them.
    public decimal GrossIncome { get; set; }

    // Benefits/income excluded from tax. Feeds both GetAnnualTaxAnnualizationInputsAsync's
    // CurrentNonTaxableBenefits and GetAlphalistAsync/Get2316DataAsync's NonTaxableIncome;
    // TaxableIncome is derived downstream at consolidation time (Gross - NonTaxable -
    // Statutory) rather than stored here — same precedent as PriorEmployerTaxRecord.
    public decimal NonTaxableIncome { get; set; }

    // Statutory deductions
    public decimal SSSContribution { get; set; }
    public decimal PhilHealthContribution { get; set; }
    public decimal PagIbigContribution { get; set; }
    public decimal WithholdingTax { get; set; }
    public decimal OtherDeductions { get; set; }
    // Computed server-side: SSS + PhilHealth + PagIbig + WithholdingTax + OtherDeductions.
    public decimal TotalDeductions { get; set; }

    // Computed server-side: GrossIncome - TotalDeductions.
    public decimal NetPay { get; set; }

    // Get-only, no setter — EF Core excludes it from the model automatically, so it's never
    // persisted. Used by GetAlphalistAsync/Get2316DataAsync to fold this row's contribution
    // into TaxableCompensation the same way Payroll's own TaxableIncome column is computed
    // upstream (Gross - NonTaxable - Statutory).
    public decimal DerivedTaxableIncome =>
        GrossIncome - NonTaxableIncome - SSSContribution - PhilHealthContribution - PagIbigContribution;
}
