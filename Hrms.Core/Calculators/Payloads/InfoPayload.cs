public class AbsentInfo
{
    public DateOnly PayrollDate { get; set; }
    public int Count { get; set; }
    public decimal Amount { get; set; }
}
public class TimeInfo
{
    public DateOnly PayrollDate { get; set; }
    public decimal Hour { get; set; }
    public decimal Amount { get; set; }
}
public class UnderTimeInfo : TimeInfo
{
    public DateOnly PayrollDate { get; set; }
    public decimal Hour { get; set; }
    public decimal Amount { get; set; }
}
public class LateInfo : TimeInfo
{
}
public class OvertimeInfo : TimeInfo
{
    public string Handler { get; set; }
}
public class NightDiffInfo : OvertimeInfo;
public class LeaveInfo
{
    public Guid LeaveId { get; set; }
    public HolidayType Type { get; set; }
    public double ConsumeCredit { get; set; }
    public decimal leaveFraction { get; set; }
}
public enum DeductionInfoType
{
    Others
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
    public IncomeClassType Type { get; set; }
    public DateOnly PayrollDate { get; set; }
    public decimal Amount { get; set; }
    public bool Taxable { get; set; }
}
