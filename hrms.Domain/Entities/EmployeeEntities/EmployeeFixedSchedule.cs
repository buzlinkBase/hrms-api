using BuzlinkRepository;

namespace Hrms.Domain.Entities.EmployeeEntities;

// One row per (EmployeeId, DayName) that has a shift assigned — sparse by design,
// so a day with no row is simply unassigned. Deleting the row unassigns that day.
[DisableSoftDelete]
public class EmployeeFixedSchedule : BaseEntity
{
    public Guid EmployeeId { get; set; }
    public virtual Employee? Employee { get; set; }
    public DayName DayName { get; set; }
    public Guid TimeShiftId { get; set; }
    public virtual TimeShift? TimeShift { get; set; }
}
