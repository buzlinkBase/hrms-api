namespace Hrms.Domain.ValueObjects;

public class CreateDepartment
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid? HeadId { get; set; }
    public string Status { get; set; }
    //public string? HeadName { get; set; } = string.Empty;
}
public class UpdateDepartment : CreateDepartment
{
    public Guid Id { get; set; }
}
public class DepartmentModel : UpdateDepartment
{
    public string BranchName { get; set; }
    public string BranchHeadName { get; set; }
}