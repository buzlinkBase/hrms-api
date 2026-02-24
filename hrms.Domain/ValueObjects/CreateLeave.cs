namespace Hrms.Domain.ValueObjects;

public class CreateLeave
{
    public string Description { get; set; } = string.Empty;
    public string Remarks { get; set; } = string.Empty;
    public double Credits { get; set; }
    public bool WithPay { get; set; }
}

public class UpdateLeave : CreateLeave
{
    public Guid Id { get; set; }
}

public class LeaveModel : UpdateLeave
{
}