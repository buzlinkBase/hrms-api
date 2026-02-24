namespace Hrms.Domain.ValueObjects;

public class CreateEmploymentHistory
{
    public Guid? EmployeeId { get; set; }
    public string CompanyName { get; set; }
    public string Position { get; set; }
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
}
public class UpdateEmploymentHistory : CreateEmploymentHistory
{
    public Guid Id { get; set; }
}
public class EmploymentHistoryModel : UpdateEmploymentHistory;