namespace Hrms.Domain.ValueObjects;

public class CreatePayrollOpeningBalance
{
    public Guid EmployeeId { get; set; }
    public int Year { get; set; }
    public decimal BasicPay { get; set; }
    public decimal OvertimePay { get; set; }
    public decimal HolidayPay { get; set; }
    public decimal Allowances { get; set; }
    public decimal OtherIncome { get; set; }
    public decimal Bonuses { get; set; }
    public decimal NonTaxableIncome { get; set; }
    public decimal SSSContribution { get; set; }
    public decimal PhilHealthContribution { get; set; }
    public decimal PagIbigContribution { get; set; }
    public decimal WithholdingTax { get; set; }
    public decimal OtherDeductions { get; set; }
}

public class UpdatePayrollOpeningBalance : CreatePayrollOpeningBalance
{
    public Guid Id { get; set; }
}

public class PayrollOpeningBalanceModel : UpdatePayrollOpeningBalance
{
    public decimal GrossIncome { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal NetPay { get; set; }
}
