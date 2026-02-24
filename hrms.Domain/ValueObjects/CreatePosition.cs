namespace Hrms.Domain.ValueObjects;

public class CreatePosition
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double Rate { get; set; } = 0;
    public string Status { get; set; }

}

public class UpdatePosition : CreatePosition
{
    public Guid Id { get; set; }
}

public class PositionModel : UpdatePosition
{
}