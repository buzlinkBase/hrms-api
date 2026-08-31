namespace Hrms.Domain.Entities;

public class Company : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Contact { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int TotalWorkingDays { get; set; }
    public int TakehomePercentage { get; set; }
    public bool ApplyStatutoryOnActualMonth { get; set; } = true;
    public string TIN { get; set; } = string.Empty;

    // ── Government agency registration (BIR/SSS/PhilHealth/Pag-IBIG report headers) ─────
    public string RDOCode { get; set; } = string.Empty;
    public string SSSNumber { get; set; } = string.Empty;
    public string PhilHealthNumber { get; set; } = string.Empty;
    public string PagIbigNumber { get; set; } = string.Empty;
    public string AuthorizedSignatoryName { get; set; } = string.Empty;
    public string AuthorizedSignatoryTitle { get; set; } = string.Empty;
}

