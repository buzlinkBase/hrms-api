namespace Hrms.Domain.ValueObjects;

public class CreatePriorEmployerTaxRecord
{
    public Guid EmployeeId { get; set; }
    public int Year { get; set; }
    public bool HasPriorEmployer { get; set; }
    public string? PriorEmployerName { get; set; }
    public decimal GrossIncomeYtd { get; set; }
    public decimal NonTaxableYtd { get; set; }
    public decimal StatutoryDeductionsYtd { get; set; }
    public decimal TaxWithheldYtd { get; set; }
}

public class UpdatePriorEmployerTaxRecord : CreatePriorEmployerTaxRecord
{
    public Guid Id { get; set; }
}
public class PriorEmployerTaxRecordModel : UpdatePriorEmployerTaxRecord;
