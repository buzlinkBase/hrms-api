namespace Hrms.Domain.ValueObjects;

public class CreateEducation
{
    public Guid? EmployeeId { get; set; }
    public string SchoolName { get; set; }
    public int YearGraduated { get; set; }
}
public class UpdateEducation : CreateEducation
{
    public Guid Id { get; set; }
}
public class EducationModel : UpdateEducation;