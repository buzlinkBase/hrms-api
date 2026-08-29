namespace Hrms.Domain.ValueObjects;

public class ClientRateEntry
{
    public RateType Type { get; set; }
    public decimal Rate { get; set; }
}

public class ClientRateTableModel
{
    public RateType Type { get; set; }
    public decimal Rate { get; set; }
}
