namespace Hrms.Domain.ValueObjects;

public class CreateRateTable
{
    public RateType Type { get; set; }
    public string? ShortDescription { get; set; } = string.Empty;
    public string? Description  { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public int Remarks { get; set; }
}

public class UpdateRateTable : CreateRateTable
{
    public Guid Id { get; set; }

}
public class RateTableModel : UpdateRateTable;
