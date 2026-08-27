namespace Hrms.Domain.Entities;

public class OtherIncomeType : BaseEntity
{
    public string Description { get; set; } = string.Empty;
}

public class OtherIncome : BaseEntity
{
    public IncomeClassType IncomeClass { get; set; } = IncomeClassType.Others;
    public virtual OtherIncomeType? IncomeType { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid? IncomeTypeId { get; set; }
    public bool IsTaxable { get; set; } = false;
}
