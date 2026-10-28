namespace Hrms.Domain.Entities;

public class Holiday : BaseEntity
{
    public string Description { get; set; } = string.Empty;
    public HolidayType HolType { get; set; } = HolidayType.LEGAL;
    public HolidayWorkType WorkType { get; set; } = HolidayWorkType.NonWorking;
    public int HolYear { get; set; }
    public DateOnly HolDate { get; set; }
    public bool IsRecuring { get; set; }
    // When set together with IsRecuring = true, the holiday recurs on the Nth (or last)
    // DayOfWeek of HolDate.Month every year, instead of HolDate's fixed day (e.g.
    // National Heroes Day = "last Monday of August"). WeekOfMonth: 1-4 = first..fourth
    // occurrence, 5 = last occurrence in the month. Null on both = the existing fixed
    // month/day recurrence. See HolidayRecurrenceCalculator.ResolveNthWeekday.
    public int? WeekOfMonth { get; set; }
    public DayOfWeek? DayOfWeek { get; set; }
    public bool IsPaid { get; set; }
    // Project sites a localized special holiday applies to; empty = nationwide.
    public virtual ICollection<HolidayArea> Areas { get; set; } = new List<HolidayArea>();
}
