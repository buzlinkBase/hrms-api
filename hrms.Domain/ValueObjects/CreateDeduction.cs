namespace Hrms.Domain.ValueObjects;

public class CreateDeduction
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; }
    public bool AllowEmployeeFiling { get; set; } = true;
    public Guid? DeductionTypeId { get; set; }
}

public class UpdateDeduction : CreateDeduction
{
    public Guid Id { get; set; }
}

public class DeductionModel : UpdateDeduction
{
}

public class CreateDeductionType
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class UpdateDeductionType : CreateDeductionType
{
    public Guid Id { get; set; }
    public string? Status { get; set; }
}

public class DeductionTypeModel : UpdateDeductionType
{
}