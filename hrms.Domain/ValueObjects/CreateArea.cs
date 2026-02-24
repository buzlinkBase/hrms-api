namespace Hrms.Domain.ValueObjects;

public class CreateArea
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Status { get; set; }

}

public class UpdateArea : CreateArea
{
    public Guid Id { get; set; }
}

public class AreaModel : UpdateArea
{
}