namespace Hrms.Domain.ValueObjects;

public class CreatePHIC
{
    public DateOnly EffectiveDate { get; set; }
    public decimal MinSalaryBase { get; set; }
    public decimal MaxSalaryBase { get; set; }
    public decimal PremiumRate { get; set; }
    public decimal EmployeeShare { get; set; }
    public decimal EmployerShare { get; set; }
    public string Remarks { get; set; } = string.Empty;
}

public class UpdatePHIC : CreatePHIC
{
    public Guid Id { get; set; }
}
public class PHICModel : UpdatePHIC
{
    public decimal TotalContribution { get; set; }
}

public class PHICContributionModel
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public DateOnly PayrollFrom { get; set; }
    public DateOnly PayrollTo { get; set; }
    public DateOnly Date { get; set; }
    public decimal EmployeeShare { get; set; }
    public decimal EmployerShare { get; set; }
    public decimal TotalContribution { get; set; }
}