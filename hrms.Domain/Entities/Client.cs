namespace Hrms.Domain.Entities;

public class Client : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string ContactPerson { get; set; } = string.Empty;

    // Setup > Client > Settings > Allowances. Null = this client doesn't give the benefit.
    // Directly-targetable fields rather than an open-ended catalog -- there are only ever these
    // two, and each has its own fixed, non-generic behavior (see below), so a generic
    // catalog-plus-config-table would only add lookup indirection for no real flexibility gain.
    // RetirementDaysPerYear is a DAY COUNT (e.g. 22.50 matches the PH RA 7641 statutory rate of
    // 22.5 days/year), not a peso amount -- consumed every payroll run by
    // EmployeePayrollLineService.ComputeRetirementAccrual: (dailyRate/8) x (regularHours x
    // RetirementDaysPerYear / 12 / 30), accumulated into RetirementFund.Balance at Post time.
    public decimal? RetirementDaysPerYear { get; set; }
    public decimal? UniformAllowance { get; set; }   // per-month rate; accrues as a balance since HireDate in a later phase
    public BenefitAccrualBasis UniformAllowanceBasis { get; set; } = BenefitAccrualBasis.TenureMonths;
}
