namespace Hrms.Domain.ValueObjects;

public class CreatePassSlipApplication
{
    public Guid EmployeeId { get; set; }
    public DateOnly ApplicationDate { get; set; }
    public DateTime DepartureTime { get; set; }
    public DateTime? ReturnTime { get; set; }
    public string Destination { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string? Remarks { get; set; }
}

public class UpdatePassSlipApplication : CreatePassSlipApplication
{
    public Guid Id { get; set; }
    public ApprovalStatus ApprovalStatus { get; set; }
}

public class PassSlipApplicationModel : UpdatePassSlipApplication
{
    public string BatchCode { get; set; } = string.Empty;
    public string? EmployeeName { get; set; }
}
