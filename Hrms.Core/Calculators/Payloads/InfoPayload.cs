public enum DeductionInfoType
{
    Others,
    Loan
}
public class DeductionInfo
{
    public DeductionInfoType Type { get; set; } = DeductionInfoType.Others;
    public Guid Id { get; set; }
    public Guid DeductionId { get; set; }
    public DateOnly PayrollDate { get; set; }
    public decimal Amount { get; set; }
    public string Remarks { get; set; } = string.Empty;
}
public class SSSInfo
{
    public Period Period { get; set; } = Period.None;
    public DateOnly PayrollDate { get; set; }
    public decimal EE { get; set; }
    //public decimal AddOn { get; set; }
    public decimal ER { get; set; }
    public decimal EC { get; set; }
    public decimal TotalER => ER + EC;
    public decimal Total => TotalER + EE;

}
public class PHICInfo
{
    public Period Period { get; set; } = Period.None;
    public DateOnly PayrollDate { get; set; }
    public decimal EE { get; set; }
    public decimal ER { get; set; }
    public decimal Total => ER + EE;
    //public decimal AddOn { get; set; }
}
public class HDMFInfo
{
    public Period Period { get; set; } = Period.None;
    public DateOnly PayrollDate { get; set; }
    public decimal EE { get; set; }
    public decimal ER { get; set; }
    public decimal Total => ER + EE;
    //public decimal AddOn { get; set; }
}
public class WTaxInfo
{
    public Period Period { get; set; } = Period.None;
    public DateOnly PayrollDate { get; set; }
    public decimal TaxableIncome { get; set; }
    public decimal TaxDue { get; set; }
}
// Shared with dtr-api\DTR.Core (project reference) — populated by DTR.Core's own
// HolidayQueryService/HolidayProviderFactory pipeline, keyed by Holidaykey (see
// HolidayService.cs), independently of CalculatorPayload/PayrollRangeContextComposerService.
public class HolidayInfo
{
    public Guid HolidayId { get; set; }
    public Guid? AreaId { get; set; }
    public DateOnly PayrollDate { get; set; }
    public HolidayWorkType WorkType { get; set; } = HolidayWorkType.Working;
    public HolidayType HolType { get; set; }
    public Guid EmployeeId { get; set; }
    public ChangeSchedState State { get; set; }
    public bool IsPaid { get; set; }

}
public class OtherIncomeInfo
{
    public Guid Id { get; set; }
    public Guid IncomeId { get; set; }
    public IncomeClassType? Type { get; set; }
    public DateOnly PayrollDate { get; set; }
    public decimal Amount { get; set; }
    public bool Taxable { get; set; }
}
