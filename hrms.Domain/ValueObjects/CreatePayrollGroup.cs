namespace Hrms.Domain.ValueObjects;

public class CutoffModel
{
    /// <summary>
    /// Day of the month when cutoff occurs (e.g., 15, 30).
    /// </summary>
    public int Day { get; set; }

    /// <summary>
    /// Indicates if this cutoff is the final cutoff of the month.
    /// Useful for months with variable end dates (28, 29, 30, 31).
    /// </summary>
    public bool IsEndOfMonth { get; set; } = false;

    /// <summary>
    /// Optional label for audit clarity (e.g., "First Cutoff", "Second Cutoff").
    /// </summary>
    public string Label { get; set; } = string.Empty;
    public Guid Id { get; set; }
}



public class CreatePayrollGroup
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public PayrollFrequency PayrollFrequency { get; set; }
    /// <summary>
    /// List of cutoff days for this payroll group.
    /// Example: Semi-monthly → [15, EndOfMonth]; Bi-weekly → [10, 25].
    /// </summary>
    public List<CutoffModel>? CutoffDays { get; set; } = new();
    public string Status { get; set; } = "Active";
}

public class UpdatePayrollGroup : CreatePayrollGroup
{
    public Guid Id { get; set; }
}
public class PayrollGroupModel  : UpdatePayrollGroup
{
}