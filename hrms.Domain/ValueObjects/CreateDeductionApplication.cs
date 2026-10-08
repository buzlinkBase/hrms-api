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
// Response-only: ApprovalStatus is read here but never accepted on Create/Update -- only the
// approve/decline endpoints (approval engine) change it. Its Breakdown carries each
// installment's Id and Balance: the edit form sends the Ids back so stored installments are
// matched (not recreated), and inheriting UpdateDeductionApplication's Id-less Breakdown
// made every edit replace -- and re-open -- the whole schedule.
public class DeductionApplicationModel
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public Guid DeductionId { get; set; }
    public DateOnly EncodeDate { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public DeductionFrequency FrequencyOfPayment { get; set; }
    public string ModeOfPayment { get; set; } = string.Empty;
    public int Terms { get; set; }
    public decimal TotalPrincipal { get; set; }
    public decimal InterestRate { get; set; }
    public decimal TotalAmount { get; set; }
    public string Note { get; set; } = string.Empty;
    public string Remarks { get; set; } = string.Empty;
    public ApprovalStatus ApprovalStatus { get; set; }
    public List<DeductionApplicationDetailModel> Breakdown { get; set; } = [];
}

public class DeductionApplicationDetailModel
{
    public Guid Id { get; set; }
    public DateOnly Date { get; set; }
    public decimal Principal { get; set; }
    public decimal Interest { get; set; }
    public decimal Amount { get; set; }
    public decimal Balance { get; set; }
    public string Status { get; set; } = string.Empty;
    public int RecordOrder { get; set; }
    public string Notes { get; set; } = string.Empty;
}

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
