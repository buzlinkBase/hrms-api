using Hrms.Domain.Entities;

namespace Hrms.Domain.ValueObjects;

public class CreateChangeHoliday
{
    public Holiday Holiday { get; set; }
    public DateOnly PayrollDateFrom { get; set; }
    public DateOnly PayrollDateTo { get; set; }
    public Guid[] EmployeeIds { get; set; }
}

public class ChangeHolidayModel
{
    public Guid BatchId { get; set; }
    public string HolidayName { get; set; }
    public string ClientName { get; set; }
    public string FullName { get; set; }
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    //public ChangeSchedState State  { get; set; }
    //public Guid EmployeeId { get; set; }
}