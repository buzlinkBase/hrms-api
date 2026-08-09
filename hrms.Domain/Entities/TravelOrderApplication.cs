using Hrms.Domain.Entities.EmployeeEntities;

namespace Hrms.Domain.Entities;
 
public class TravelOrderApplication : BaseEntity
{
    public Guid EmployeeId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public DateTime ApplicationDate { get; set; }
    public DateOnly StartDate { get; set; }//covered Date from 
    public DateOnly EndDate { get; set; } //covered Date from 

    public bool IsManualEntry { get; set; }  
    public double TotalMinutes  { get; set; }
    public DateTime? StartTime  { get; set; }
    public DateTime? EndTime  { get; set; }
     
    //public TravelDayType TravelDayType { get; set; }
    //public int Days { get; set; }
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

