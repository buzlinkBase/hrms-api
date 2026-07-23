namespace Hrms.Domain.ValueObjects;

public class CreateHDMF
{
    public DateOnly EffectiveDate { get; set; }
    public decimal MinSalaryBase { get; set; }      // Minimum salary considered (often ₱1,000)
    public decimal MaxSalaryBase { get; set; }      // Maximum salary considered (e.g., ₱5,000 for standard Pag-IBIG)
    public decimal EmployeeRate { get; set; }       // Employee contribution rate (e.g., 0.01m = 1%)
    public decimal EmployerRate { get; set; }       // Employer contribution rate (e.g., 0.02m = 2%)
    public decimal EmployeeShare { get; set; }
    public decimal EmployerShare { get; set; }
    public string Remarks { get; set; } = string.Empty;
}

public class UpdateHDMF : CreateHDMF
{
    public Guid Id { get; set; }
}
public class HDMFModel : UpdateHDMF
{
    public decimal TotalContribution { get; set; }
}

public class HDMFContributionModel
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public DateOnly PayrollFrom { get; set; }
    public DateOnly PayrollTo { get; set; }
    public DateOnly PayrollDate { get; set; }
    public decimal EmployeeShare { get; set; }
    public decimal EmployerShare { get; set; }
    public decimal TotalContribution { get; set; }
}