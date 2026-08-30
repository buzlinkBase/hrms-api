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
}

