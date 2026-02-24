namespace Hrms.Domain.Entities;

public class DeductionApplication : BaseEntity
{
    public Guid EmployeeId { get; set; }
    public Guid DeductionId { get; set; }
    public DateOnly EncodeDate { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public DeductionFrequency FrequencyOfPayment { get; set; }
    public int Terms { get; set; }
    public decimal TotalPrincipal { get; set; }
    public decimal InterestRate { get; set; }
    public decimal TotalAmount { get; set; }
    public string ProcessBy { get; set; }
    public string Note { get; set; }
    public string Remarks { get; set; }
    public virtual ICollection<DeductionApplicationDetail> Breakdown { get; set; }

}

public class DeductionApplicationDetail : BaseEntity
{
    public Guid EmployeeId { get; set; }
    public Guid DeductionId { get; set; }
    public Guid ApplicationId { get; set; }
    public DateOnly Date { get; set; }
    public decimal Principal { get; set; }
    public decimal Interest { get; set; }
    public int RecordOrder { get; set; }
    public decimal Amount { get; set; }
    public decimal Balance { get; set; }
    public string Notes { get; set; } = string.Empty;
    public DeductionApplicationDetail()
    {
        Status = "Unpaid";
    }
}


public class DeductionPayment : BaseEntity
{
    public Guid EmployeeId { get; set; }
    public Guid DeductionId { get; set; }
    public DateOnly PayrollDate { get; set; }
    public decimal Amount { get; set; }

}