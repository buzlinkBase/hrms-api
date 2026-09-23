using BuzlinkRepository;

namespace Hrms.Domain.Entities;

[DisableSoftDelete]
public class PHICTable : BaseEntity
{
    public DateOnly EffectiveDate { get; set; }
    public decimal MinSalaryBase { get; set; }
    public decimal MaxSalaryBase { get; set; }
    public decimal PremiumRate { get; set; }
    public decimal EmployeeShare { get; set; }
    public decimal EmployerShare { get; set; }
    public decimal TotalContribution { get; set; }
    public string Remarks { get; set; } = string.Empty;
}


[DisableSoftDelete]
public class PHICContribution : BaseEntity, IDateFilter
{
    public Guid PayrollBatchId { get; set; }
    public Guid EmployeeId { get; set; }
    public DateOnly PayrollFrom { get; set; }
    public DateOnly PayrollTo { get; set; }
    public DateOnly PayrollDate { get; set; }
    public decimal EmployeeShare { get; set; }
    public decimal EmployerShare { get; set; }
    public decimal TotalContribution { get; set; }
}