namespace Hrms.Domain.ValueObjects;

public class CreateCompany
{
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;

}
public class UpdateCompany : CreateCompany
{
    public Guid Id { get; set; }
}
public class CompanyModel : UpdateCompany;
