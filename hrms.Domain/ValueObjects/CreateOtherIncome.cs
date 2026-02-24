
namespace Hrms.Domain.ValueObjects;

public class CreateOtherIncome
{
    public IncomeClassType IncomeClass { get; set; } = IncomeClassType.Others;
    public Guid? IncomeTypeId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsTaxable { get; set; } = false;
    public string Status { get; set; }

}

public class UpdateOtherIncome : CreateOtherIncome
{
    public Guid Id { get; set; }
}

public class OtherIncomeModel : UpdateOtherIncome
{
}


public class CreateOtherIncomeType
{
    public string Description { get; set; } = string.Empty;
}

public class UpdateOtherIncomeType : CreateOtherIncomeType
{
    public Guid Id { get; set; }
}
public class OtherIncomeTypeModel : UpdateOtherIncomeType
{

}