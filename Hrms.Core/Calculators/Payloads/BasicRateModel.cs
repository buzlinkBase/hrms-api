namespace Hrms.Core.Calculators.Payloads;

public class BasicRateModel
{
    public DateOnly Date { get; set; }//per date computation
    public Guid EmployeeId { get; set; }
    public decimal Basic { get; set; }
    public decimal RegularDuty { get; set; }
    public decimal RestDayDuty { get; set; }
    public decimal LWOP { get; set; }
    public decimal LeaveWithPay { get; set; }
    public decimal TimeBaseGross { get; set; }
    public decimal LegalHoliday { get; set; }
    public decimal SpecialHoliday { get; set; }
    public AbsentInfo AbsentInfo { get; set; } = new();
    public List<LeaveInfo> Leaves { get; set; } = new();
    public LateInfo LateHourInfo { get; set; } = new();
    public UnderTimeInfo UTHourInfo { get; set; } = new();
    public OvertimeInfo OTHourInfo { get; set; } = new();
    public NightDiffInfo NightDiffInfo { get; set; } = new();
    public string Remarks { get; set; } = string.Empty;

}
