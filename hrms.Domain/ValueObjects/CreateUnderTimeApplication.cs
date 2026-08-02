namespace Hrms.Domain.ValueObjects;

public class CreateUnderTimeApplication
{
    public Guid EmployeeId { get; set; }
    public DateOnly PayrollDate { get; set; }
    public double UTMinutes { get; set; }
    public string Remarks { get; set; } = string.Empty;
}

public class UpdateUnderTimeApplication : CreateUnderTimeApplication
{
    public Guid Id { get; set; }
    public ApprovalStatus ApprovalStatus { get; set; }
}

public class UnderTimeApplicationModel : UpdateUnderTimeApplication { }
