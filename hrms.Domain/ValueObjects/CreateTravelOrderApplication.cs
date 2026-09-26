namespace Hrms.Domain.ValueObjects;

public class CreateTravelOrderApplication
{
    public Guid EmployeeId { get; set; }
    public DateTime ApplicationDate { get; set; }
    public DateOnly StartDate { get; set; }//covered Date from 
    public DateOnly EndDate { get; set; } //covered Date from 
    public bool IsManualEntry { get; set; }
    public double TotalMinutes { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string Destination { get; set; } = string.Empty;
    public string Classification { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public double Cost { get; set; } = 0;
    public string? ApplicationRemarks { get; set; }
}

public class UpdateTravelOrderApplication : CreateTravelOrderApplication
{
    public Guid Id { get; set; }
    public ApprovalStatus ApprovalStatus { get; set; }= ApprovalStatus.Approved;
    // Approver's note for an approve/decline transition — see LeaveApplication's UpdateLeaveApplication.Note.
    public string? Note { get; set; }
}

public class TravelOrderApplicationModel : UpdateTravelOrderApplication
{
    // "Filed On" on the portal's My Official Business list.
    public DateTime CreatedAt { get; set; }
}
