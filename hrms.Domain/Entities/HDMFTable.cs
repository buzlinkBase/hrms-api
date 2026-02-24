using BuzlinkRepository;

namespace Hrms.Domain.Entities;

[DisableSoftDelete]
public class HDMFTable : BaseEntity
{
    public DateOnly EffectiveDate { get; set; }
    public decimal MinSalaryBase { get; set; }      // Minimum salary considered (often ₱1,000)
    public decimal MaxSalaryBase { get; set; }      // Maximum salary considered (e.g., ₱5,000 for standard Pag-IBIG)
    public decimal EmployeeRate { get; set; }       // Employee contribution rate (e.g., 0.01m = 1%)
    public decimal EmployerRate { get; set; }       // Employer contribution rate (e.g., 0.02m = 2%)

    // 💰 Computed Contributions
    public decimal EmployeeShare { get; set; }
    public decimal EmployerShare { get; set; }
    public decimal TotalContribution { get; set; }
    public string Remarks { get; set; } = string.Empty;

    // ⚙️ Methods
    //public void CalculateContribution(decimal actualSalary)
    //{
    //    // Apply min/max salary base rules
    //    decimal baseSalary = actualSalary;
    //    if (baseSalary < MinSalaryBase) baseSalary = MinSalaryBase;
    //    if (baseSalary > MaxSalaryBase) baseSalary = MaxSalaryBase;

    //    // Compute contributions
    //    EmployeeShare = baseSalary * EmployeeRate;
    //    EmployerShare = baseSalary * EmployerRate;
    //}

}

public class HDMFContribution : BaseEntity,IDateFilter
{
    public Guid EmployeeId { get; set; }
    public DateOnly PayrollFrom { get; set; }
    public DateOnly PayrollTo { get; set; }
    public DateOnly PayrollDate { get; set; }
    public decimal EmployeeShare { get; set; }
    public decimal EmployerShare { get; set; }
    public decimal TotalContribution { get; set; }
}