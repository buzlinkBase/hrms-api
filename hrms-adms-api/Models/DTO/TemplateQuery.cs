namespace Hrms.adms.Models.DTO;

public class TemplateQuery
{
    public string SN { get; set; }
    public int BioId { get; set; }
    public Guid DepartmentId { get; set; }
    public Guid ClientId { get; set; }
    public Guid EmployeeId  { get; set; }

}
