namespace Hrms.Domain.ValueObjects;

public class CreateTravelOrderApplication
{
    public Guid EmployeeId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public TravelDayType TravelDayType { get; set; } = TravelDayType.WholeDay;
    public string Destination { get; set; } = string.Empty;
    public string Classification { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public double Cost { get; set; }
    public string? ApplicationRemarks { get; set; }
}

public class UpdateTravelOrderApplication : CreateTravelOrderApplication
{
    public Guid Id { get; set; }
    public string ApprovalStatus { get; set; } = "ForApproval";
}

public class TravelOrderApplicationModel : UpdateTravelOrderApplication
{
    public int Days { get; set; }
    public DateOnly ApplicationDate { get; set; }
    public string? Reference { get; set; }
}
