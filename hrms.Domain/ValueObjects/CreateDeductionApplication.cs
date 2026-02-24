namespace Hrms.Domain.ValueObjects;

public class CreateDeductionApplication
{
    public Guid EmployeeId { get; set; }
    public Guid DeductionId { get; set; }
    public DateOnly EncodeDate { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public DeductionFrequency FrequencyOfPayment { get; set; } = DeductionFrequency.SemiMonthly;
    public int Terms { get; set; }
    public decimal TotalPrincipal { get; set; }
    public decimal InterestRate { get; set; }
    public decimal TotalAmount { get; set; }
    public string Note { get; set; } = string.Empty;
    public string Remarks { get; set; } = string.Empty;
    public List<CreateDeductionApplicationDetail> Breakdown { get; set; }
}

public class UpdateDeductionApplication : CreateDeductionApplication
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public Guid DeductionId { get; set; }
    public DateOnly EncodeDate { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string ModeOfPayment { get; set; } = string.Empty;
    public int Terms { get; set; }
    public decimal TotalPrincipal { get; set; }
    public decimal InterestRate { get; set; }
    public decimal TotalAmount { get; set; }
    public string Note { get; set; } = string.Empty;
    public string Remarks { get; set; } = string.Empty;
    public List<UpdateDeductionApplicationDetail> Details { get; set; }

}
public class DeductionApplicationModel : UpdateDeductionApplication;

public class CreateDeductionApplicationDetail
{
    public DateOnly Date { get; set; }
    public decimal Principal { get; set; }
    public decimal Interest { get; set; }
    public decimal Amount { get; set; }
}

public class UpdateDeductionApplicationDetail
{
    public Guid Id { get; set; }
    public DateOnly Date { get; set; }
    public decimal Principal { get; set; }
    public decimal Interest { get; set; }
    public decimal Amount { get; set; }
}
