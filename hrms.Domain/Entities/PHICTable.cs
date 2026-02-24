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
    //// ⚙️ Methods
    //public void CalculateContribution(decimal actualSalary)
    //{
    //    // Apply min/max salary base rules
    //    decimal baseSalary = actualSalary;
    //    if (baseSalary < MinSalaryBase) baseSalary = MinSalaryBase;
    //    if (baseSalary > MaxSalaryBase) baseSalary = MaxSalaryBase;

    //    // Compute contributions
    //    decimal premium = baseSalary * PremiumRate;
    //    EmployeeShare = premium / 2;   // Split equally (default rule)
    //    EmployerShare = premium / 2;
    //}
}


public class PHICContribution : BaseEntity,IDateFilter
{
    public Guid EmployeeId { get; set; }
    public DateOnly PayrollFrom { get; set; }
    public DateOnly PayrollTo { get; set; }
    public DateOnly PayrollDate { get; set; }
    public decimal EmployeeShare { get; set; }
    public decimal EmployerShare { get; set; }
    public decimal TotalContribution { get; set; }
}