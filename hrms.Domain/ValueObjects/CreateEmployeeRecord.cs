namespace Hrms.Domain.ValueObjects;

public class CreateEmployeeRecord
{
    public Guid? EmployeeId { get; set; }
    public string RecordType { get; set; }
    public string Description { get; set; }
    public string File { get; set; }
}

public class UpdateEmployeeRecord : CreateEmployeeRecord
{
    public Guid Id { get; set; }
}
public class EmployeeRecordModel : UpdateEmployeeRecord;