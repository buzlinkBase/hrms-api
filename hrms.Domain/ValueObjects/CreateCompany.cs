namespace Hrms.Domain.ValueObjects;

public class CreateCompany
{
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Contact { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string TIN { get; set; } = string.Empty;
    public string RDOCode { get; set; } = string.Empty;
    public string SSSNumber { get; set; } = string.Empty;
    public string PhilHealthNumber { get; set; } = string.Empty;
    public string PagIbigNumber { get; set; } = string.Empty;
    public string AuthorizedSignatoryName { get; set; } = string.Empty;
    public string AuthorizedSignatoryTitle { get; set; } = string.Empty;

}
public class UpdateCompany : CreateCompany
{
    public Guid Id { get; set; }
}
public class CompanyModel : UpdateCompany;
