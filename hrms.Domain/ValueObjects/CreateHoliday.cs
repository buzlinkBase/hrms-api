namespace Hrms.Domain.ValueObjects;

public class CreateHoliday
{
    public string Description { get; set; } = string.Empty;
    public HolidayType HolType { get; set; } = HolidayType.LEGAL;
    public HolidayWorkType WorkType { get; set; } = HolidayWorkType.NonWorking;
    public DateOnly HolDate { get; set; }
    public bool IsRecuring { get; set; }
    public bool IsPaid { get; set; }
    public Guid? AreaId { get; set; }
    public string Status { get; set; }
}
public class UpdateHoliday : CreateHoliday
{
    public Guid Id { get; set; }
}
public class HolidayModel : UpdateHoliday
{
    public int HolYear { get; set; }
}
