namespace Hrms.Domain.ValueObjects;

public class CreatePayrollInclusionDefaults
{
    public bool DefaultRestDayPaid { get; set; }
    public bool DefaultRegularHolidayIncluded { get; set; }
    public bool DefaultSpecialNonWorkingIncluded { get; set; }
}
public class UpdatePayrollInclusionDefaults : CreatePayrollInclusionDefaults
{
    public Guid Id { get; set; }
}
public class PayrollInclusionDefaultsModel : UpdatePayrollInclusionDefaults;
