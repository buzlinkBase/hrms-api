namespace Hrms.Domain.ValueObjects;

public abstract class OtherIncomeApplicationBase
{
    public Guid EmployeeId { get; set; }
    public DateOnly EncodeDate { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public AllowanceFrequency FrequencyOfPayment { get; set; } = AllowanceFrequency.SemiMonthly;
    public Guid IncomeId { get; set; }
    public bool IsProrated { get; set; }
    public bool IsTaxable { get; set; }
    public decimal Amount { get; set; }
    public string Remarks { get; set; } = string.Empty;

}
public class CreateOtherIncomeApplication : OtherIncomeApplicationBase
{
    public virtual ICollection<CreateOtherIncomeSchedule> Schedule { get; set; }
}

public class UpdateOtherIncomeApplication : OtherIncomeApplicationBase
{
    public Guid Id { get; set; }
    public virtual ICollection<UpdateOtherIncomeSchedule> Schedule { get; set; }

}

public class OtherIncomeApplicationModel : UpdateOtherIncomeApplication { }

public class CreateOtherIncomeSchedule
{
    public DateOnly Date { get; set; }
    public decimal Amount { get; set; }
}

public class UpdateOtherIncomeSchedule : CreateOtherIncomeSchedule
{
    public Guid Id { get; set; }
}