
namespace Hrms.Core.Calculators.Payloads;

public class PayrollSummaryLine
{
    public DateOnly PayPeriodStart { get; set; }
    public DateOnly PayPeriodEnd { get; set; }
    public DateOnly PayrollDate { get; set; }
    public Guid BatchCode { get; set; }
    public Guid EmployeeId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string PayrollPeriod { get; set; } = string.Empty;

    // Earnings
    public decimal BasicSalary { get; set; }
    public decimal OvertimeHour { get; set; }
    public decimal OvertimePay { get; set; }
    public decimal NightDifferentialHour { get; set; }
    public decimal NightDifferentialPay { get; set; }
    public decimal HolidayPay { get; set; }

    // Per-type basic pay breakdown (sum of HolidayPay = LegalHolidayPay + SpecialHolidayPay +
    // RestLegalDayPay + RestSpecialDayPay + SpecialWorkDayPay + DoubleLegalPay + RestDoubleLegalPay)
    public decimal RegularPay   { get; set; }
    public decimal RestDayPay { get; set; }
    public decimal LegalHolidayPay { get; set; }
    public decimal SpecialHolidayPay { get; set; }
    public decimal RestLegalDayPay { get; set; }
    public decimal RestSpecialDayPay { get; set; }
    public decimal SpecialWorkDayPay { get; set; }
    public decimal DoubleLegalPay { get; set; }
    public decimal RestDoubleLegalPay { get; set; }

    // Per-category OT/ND/NDOT pay (sum of each group = OvertimePay / NightDifferentialPay above)
    public decimal RegularOTPay { get; set; }
    public decimal RestDayOTPay { get; set; }
    public decimal LegalHolOTPay { get; set; }
    public decimal RestLegalDayOTPay { get; set; }
    public decimal SpecialWorkingOTPay { get; set; }
    public decimal SpecialNonWorkingOTPay { get; set; }
    public decimal RestSpecialDayOTPay { get; set; }
    public decimal SpecialWorkDayOTPay { get; set; }
    public decimal DoubleLegalOTPay { get; set; }
    public decimal RestDoubleLegalOTPay { get; set; }

    public decimal RegularNDPay { get; set; }
    public decimal RestDayNDPay { get; set; }
    public decimal LegalHolNDPay { get; set; }
    public decimal RestLegalDayNDPay { get; set; }
    public decimal SpecialNonWorkingNDPay { get; set; }
    public decimal RestSpecialDayNDPay { get; set; }
    public decimal DoubleLegalNDPay { get; set; }
    public decimal RestDoubleLegalNDPay { get; set; }

    public decimal RegularNDOTPay { get; set; }
    public decimal RestDayNDOTPay { get; set; }
    public decimal LegalHolNDOTPay { get; set; }
    public decimal RestLegalDayNDOTPay { get; set; }
    public decimal SpecialNonWorkingNDOTPay { get; set; }
    public decimal RestSpecialDayNDOTPay { get; set; }
    public decimal DoubleLegalNDOTPay { get; set; }
    public decimal RestDoubleLegalNDOTPay { get; set; }

    public List<ProratedAllowanceModel> RegularAllowanceProrated { get; set; } = new(); //For SSS
    public decimal Cola { get; set; }
    public decimal TotalRegularAllowances { get; set; }
    public decimal TotalBonuses { get; set; }
    public decimal Reimbursement { get; set; }
    public decimal TotalOtherIncome { get; set; }
    public decimal TotalDeminimises { get; set; }
    public decimal TotalCommissions { get; set; }
    public decimal GrossIncome { get; set; } //BasicSalary+OT+ND+HolidayPay+cola+otherIncomeTaxableandNonTaxable

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
    public decimal Absences { get; set; }
    public decimal AbsentCount { get; set; }
    public decimal LateAmount { get; set; }
    public decimal LateHours { get; set; }
    public decimal UnderTimeAmount { get; set; }
    public decimal UnderTimeHours { get; set; }

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
    public List<OtherIncomeInfo> OtherIncomeCollection { get; set; } = new();
    public List<BasicRateModel> BasicSalaryItems { get; set; } = new();
    public List<DeductionInfo> DeductionCollection { get; set; } = new();

    // Per-type DTR hour breakdown (aggregated across the payroll period)
    public double RegularNetHours { get; set; }
    public double RegularOTHours { get; set; }
    public double RegularNDHours { get; set; }
    public double RegularNDOTHours { get; set; }

    public double RestDayHours { get; set; }
    public double RestDayOTHours { get; set; }
    public double RestDayNDHours { get; set; }
    public double RestDayNDOTHours { get; set; }

    public double LegalHolHours { get; set; }
    public double LegalHolOTHours { get; set; }
    public double LegalHolNightDiffHours { get; set; }
    public double LegalHolNightDiffOTHours { get; set; }

    public double SpecialHolHours { get; set; }
    public double SpecialHolOTHours { get; set; }
    public double SpecialHolNightDiffHours { get; set; }
    public double SpecialHolNightDiffOTHours { get; set; }

    public double RestLegalDayHours { get; set; }
    public double RestLegalDayOTHours { get; set; }
    public double RestLegalDayNDHours { get; set; }
    public double RestLegalDayNDOTHours { get; set; }

    public double RestSpecialDayHours { get; set; }
    public double RestSpecialDayOTHours { get; set; }
    public double RestSpecialDayNDHours { get; set; }
    public double RestSpecialDayNDOTHours { get; set; }

    public double SpecialWorkDayHours { get; set; }
    public double SpecialWorkDayOTHours { get; set; }
    public double SpecialWorkDayNDHours { get; set; }
    public double SpecialWorkDayNDOTHours { get; set; }

    public double DoubleLegalHours { get; set; }
    public double DoubleLegalOTHours { get; set; }
    public double DoubleLegalNDHours { get; set; }
    public double DoubleLegalNDOTHours { get; set; }

    public double RestDoubleLegalHours { get; set; }
    public double RestDoubleLegalOTHours { get; set; }
    public double RestDoubleLegalNDHours { get; set; }
    public double RestDoubleLegalNDOTHours { get; set; }
}
