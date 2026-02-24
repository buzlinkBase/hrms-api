using Hrms.Domain.Entities.EmployeeEntities;

namespace Hrms.Domain.Entities;



public class TravelOrderApplication : BaseEntity
{
    public Guid EmployeeId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public DateTime ApplicationDate { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public TravelDayType TravelDayType { get; set; }
    public int Days { get; set; }
    public string Destination { get; set; } = string.Empty;
    public string Classification { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public double Cost { get; set; }
    public virtual Employee? Employee { get; set; }
    public TravelOrderApplication()
    {
        Status = "For Approval";
    }
}

