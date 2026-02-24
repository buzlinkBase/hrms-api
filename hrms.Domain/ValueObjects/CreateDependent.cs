namespace Hrms.Domain.ValueObjects;

public class CreateDependent
{
    public Guid? EmployeeId { get; set; }
    public string FullName { get; set; }
    public string Relationship { get; set; }
    public string Gender { get; set; }
    public DateOnly DOB { get; set; }
}

public class UpdateDependent : CreateDependent
{
    public Guid Id { get; set; }
}
public class DependentModel : UpdateDependent;