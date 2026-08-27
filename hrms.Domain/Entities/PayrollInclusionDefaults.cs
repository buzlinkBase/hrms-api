namespace Hrms.Domain.Entities;

public class PayrollInclusionDefaults : BaseEntity
{
    public bool DefaultRestDayPaid { get; set; }
    public bool DefaultRegularHolidayIncluded { get; set; }
    public bool DefaultSpecialNonWorkingIncluded { get; set; }
    public bool DefaultNightDiffIncluded { get; set; }
}
