namespace Hrms.Domain.Entities;

public class DeductionType : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class Deduction : BaseEntity
{
    public Guid? CategoryId { get; set; }
    public virtual DeductionType? Category { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int PriorityLevel { get; set; }
    public bool AllowEmployeeFiling { get; set; } = true; // false = hidden from Employee Portal, admin-only
    // true = applications of this deduction may use the attendance pay modes (per qualifying day,
    // weekly/monthly gate). Off by default: it only makes sense for a flat charge, never a loan.
    public bool AllowAttendanceRules { get; set; }
}
