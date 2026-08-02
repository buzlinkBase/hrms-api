namespace Hrms.Domain.ValueObjects;

public class CreateLeave
{
    public string Code { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string Description { get; set; } = string.Empty;
    public double Credits { get; set; }
    public PaySource PaySource { get; set; }
    public LeaveReset LeaveReset { get; set; } = LeaveReset.PerPeriod;
    public string Remarks { get; set; } = string.Empty;
}

public class UpdateLeave : CreateLeave
{
    public Guid Id { get; set; }
}

public class LeaveModel : UpdateLeave
{
}
