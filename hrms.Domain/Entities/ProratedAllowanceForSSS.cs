namespace Hrms.Domain.Entities;


public class ProratedAllowanceForSSS : BaseEntity, IPostedFilter, IDateFilter
{
    public DateOnly PayrollFrom { get; set; }
    public DateOnly PayrollTo { get; set; }
    public DateOnly PayrollDate { get; set; }
    public Guid EmployeeId { get; set; }
    public decimal Amount { get; set; }
    public bool IsPosted { get; set; }

}