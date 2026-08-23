namespace Hrms.Domain.Entities;

public class PayrollGroup : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public PayrollFrequency PayrollFrequency { get; set; }
    public virtual ICollection<CutoffDay>? CutoffDays { get; set; }

}

public class CutoffDay : BaseEntity
{
    public Guid PayrollGroupId { get; set; }

    public virtual PayrollGroup PayrollGroup { get; set; }
    /// <summary>
    /// Day of the month when cutoff occurs (e.g., 15, 30).
    /// </summary>
    public int Day { get; set; } = 1;

    /// <summary>
    /// Indicates if this cutoff is the final cutoff of the month.
    /// Useful for months with variable end dates (28, 29, 30, 31).
    /// </summary>
    public bool IsEndOfMonth { get; set; } = false;

    /// <summary>
    /// Optional label for audit clarity (e.g., "First Cutoff", "Second Cutoff").
    /// </summary>
    public string Label { get; set; } = string.Empty;
}
