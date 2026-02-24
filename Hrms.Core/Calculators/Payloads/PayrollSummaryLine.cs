
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

    //public decimal ThirteenthMonthPay { get; set; }
    public decimal NonTaxableBenefits { get; set; } // e.g., de minimis benefits
    public decimal TaxableBenefits { get; set; } // e.g., allowances exceeding thresholds
    public List<OtherIncomeInfo> OtherIncomeCollection { get; set; } = new();
    public List<BasicRateModel> BasicSalaryItems { get; set; } = new();//store all calculated basic we will use this in gov stat computation
    public List<DeductionInfo> DeductionCollection { get; set; } = new();
}
