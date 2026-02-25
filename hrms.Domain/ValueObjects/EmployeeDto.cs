namespace Hrms.Domain.ValueObjects;

public class EmployeeDto
{
    public Guid? Id { get; set; }
    public int BioId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string MiddleName { get; set; } = string.Empty;
    public string Suffix { get; set; } = string.Empty;
    public string EmployeeNo { get; set; } = string.Empty;
    public string DepartmentName  { get; set; } = string.Empty;
    public Guid? TimeShiftId { get; set; }
    public Guid? AreaId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? ClientId { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? PayrollGroupId { get; set; }
    public Guid? PositionId { get; set; }
    public Guid? SectionId { get; set; }

    public List<RestDayDto> RestDays { get; set; } = new();
}
public class RestDayDto
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; } 
    public DayName DayName { get; set; }  
}
