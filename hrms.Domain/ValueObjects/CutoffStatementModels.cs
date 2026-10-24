namespace Hrms.Domain.ValueObjects;

// Cutoff Statement: everything one employee was paid in a cutoff across ALL their per-client runs,
// with the run that took their deductions marked. See PayrollService.GetCutoffStatementAsync.
public class CutoffStatementModel
{
    public Guid EmployeeId { get; set; }
    public string? FullName { get; set; }
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public List<CutoffStatementRun> Runs { get; set; } = [];
    // Sum of the runs below, so the employee sees one total for the cutoff.
    public CutoffStatementTotals Totals { get; set; } = new();
}

public class CutoffStatementRun
{
    public Guid PayrollBatchId { get; set; }
    public Guid? ClientId { get; set; }
    public DateOnly PayPeriodStart { get; set; }
    public DateOnly PayPeriodEnd { get; set; }
    public DateOnly? PayDate { get; set; }
    public string? Remarks { get; set; }
    public bool IsPosted { get; set; }
    public string ApprovalStatus { get; set; } = string.Empty;
    // False = earnings only; another run of the cutoff took the deductions.
    public bool IsDeductionCarrier { get; set; }
    public decimal GrossIncome { get; set; }
    public decimal SssContribution { get; set; }
    public decimal PhilHealthContribution { get; set; }
    public decimal PagIbigContribution { get; set; }
    public decimal WithholdingTax { get; set; }
    public decimal CashBondDeduction { get; set; }
    public decimal TotalLoans { get; set; }
    // Deductions other than loans (OtherDeductions holds loans too, so loans are taken out).
    public decimal OtherDeductions { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal NetPay { get; set; }
}

public class CutoffStatementTotals
{
    public decimal GrossIncome { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal NetPay { get; set; }
}
