

using BuzlinkRepository;

namespace Hrms.Domain.Entities;

[DisableSoftDelete]
public class SSSTable : BaseEntity
{

    public DateOnly EffectiveDate { get; set; }
    public decimal RangeFrom { get; set; }
    public decimal RangeTo { get; set; }
    public decimal MSC { get; set; }
    public decimal EE { get; set; }
    public decimal ER { get; set; }
    public decimal EC { get; set; }
    public decimal TotalContibution { get; set; }

}

[DisableSoftDelete]
public class SSSContribution : BaseEntity, IDateFilter
{
    // Real FK to PayrollBatch.Id, mirroring Payroll.PayrollBatchId — lets a whole run's
    // contribution rows be deleted in one statement instead of looping per employee, and
    // removes the ambiguity of matching on EmployeeId+PayrollFrom+PayrollTo alone (two
    // different batches covering the same employee+period would otherwise collide on delete).
    public Guid PayrollBatchId { get; set; }
    public Guid EmployeeId { get; set; }
    public DateOnly PayrollFrom { get; set; }
    public DateOnly PayrollTo { get; set; }
    public DateOnly PayrollDate { get; set; }
    public decimal EE { get; set; }
    public decimal ER { get; set; }
    public decimal EC { get; set; }
    public decimal TotalContibution { get; set; }
    public string Remarks { get; set; } = string.Empty;
}