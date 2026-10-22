using Hrms.Domain.Entities;

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
    public string IndustryCode { get; set; } = string.Empty;
    public string SubIndustryCode { get; set; } = string.Empty;
    public string BusinessType { get; set; } = string.Empty;
    public string CompanySize { get; set; } = string.Empty;
    public string RegisteredName { get; set; } = string.Empty;
    public string Website { get; set; } = string.Empty;
    public string Barangay { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string Country { get; set; } = CompanyDefaults.Country;
    public string SecondaryPhone { get; set; } = string.Empty;
    public string HrContactName { get; set; } = string.Empty;
    public string HrContactEmail { get; set; } = string.Empty;
    public string HrContactPhone { get; set; } = string.Empty;
    public string PayrollContactName { get; set; } = string.Empty;
    public string PayrollContactEmail { get; set; } = string.Empty;
    public string PayrollContactPhone { get; set; } = string.Empty;
    /// <summary>private, government or household.</summary>
    public string EmployerType { get; set; } = string.Empty;
    public bool IsWithholdingAgent { get; set; }
    public string AtcCode { get; set; } = string.Empty;
    public string TinBranchCode { get; set; } = string.Empty;
    public string SssBranchCode { get; set; } = string.Empty;
    public string PhilHealthBranchCode { get; set; } = string.Empty;
    public string PagIbigBranchCode { get; set; } = string.Empty;
    public string SecDtiNumber { get; set; } = string.Empty;
    public DateTime? RegistrationDate { get; set; }
    public string BusinessPermitNo { get; set; } = string.Empty;
    public DateTime? BusinessPermitExpiry { get; set; }
    public string PayslipFooter { get; set; } = string.Empty;
    public bool ShowLetterhead { get; set; } = true;
    /// <summary>"Off" or "Auto". The running counter is deliberately not here: a save must never reset it.</summary>
    public string EmployeeIdMode { get; set; } = EmployeeIdSettings.Off;
    public string EmployeeIdPrefix { get; set; } = string.Empty;
    public int EmployeeIdPadding { get; set; } = EmployeeIdSettings.DefaultPadding;
    public bool EmployeeIdIncludeYear { get; set; }
    public string TimeZone { get; set; } = CompanyDefaults.TimeZone;
    public string Currency { get; set; } = CompanyDefaults.Currency;
    /// <summary>Comma-separated weekday names, e.g. "Saturday,Sunday".</summary>
    public string WorkWeekRestDays { get; set; } = CompanyDefaults.RestDays;
}
public class UpdateCompany : CreateCompany
{
    public Guid Id { get; set; }
}
public class CompanyModel : UpdateCompany
{
    /// <summary>A short-lived link to show the logo; empty when there's none.</summary>
    public string LogoUrl { get; set; } = string.Empty;
    /// <summary>Read-only: the next employee number the counter will hand out (for the example on the settings screen).</summary>
    public int NextEmployeeNo { get; set; } = 1;
}
