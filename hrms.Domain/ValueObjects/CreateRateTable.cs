namespace Hrms.Domain.ValueObjects;

public class CreateRateTable
{
    public RateType Type { get; set; }
    public decimal Rate { get; set; }
    public int Remarks { get; set; }
}

public class UpdateRateTable : CreateRateTable
{
    public Guid Id { get; set; }

}
public class RateTableModel : UpdateRateTable;
