namespace Hrms.Domain.ValueObjects;

public class CreateSection
{
    public Guid? DepartmentId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; }
}

public class UpdateSection : CreateSection
{
    public Guid Id { get; set; }
}
public class SectionModel : UpdateSection
{
    public string? DepartmentName { get; set; }
}