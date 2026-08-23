namespace Hrms.Core.Calculators.Payloads;

public class BasicRateModel
{
    public Guid? DtrId  { get; set; }
    public string? DTRRef  { get; set; }
    public DateOnly Date { get; set; } 
    public Guid EmployeeId { get; set; }
    public decimal BasicPay { get; set; }
    public decimal RegularDuty { get; set; }
    public decimal RestDayDuty { get; set; }
    public decimal LWOP { get; set; }
    public decimal LeaveWithPay { get; set; }
    public decimal TimeBaseGross { get; set; }
    public decimal LegalHoliday { get; set; }
    public decimal RestLegalDay { get; set; }
    public decimal RestSpecialDay { get; set; }
    public decimal SpecialWorkDay { get; set; }
    public decimal DoubleLegal { get; set; }
    public decimal RestDoubleLegal { get; set; }

    // Per-category OT/ND/NDOT pay — one dedicated pipeline per column, individually traceable
    // instead of folded into the aggregate OTHourInfo/NightDiffInfo below.
    public decimal RegularOT { get; set; }
    public decimal RestDayOT { get; set; }
    public decimal LegalHolOT { get; set; }
    public decimal RestLegalDayOT { get; set; }
    public decimal SpecialNonWorkingOT { get; set; }
    public decimal RestSpecialDayOT { get; set; }
    public decimal DoubleLegalOT { get; set; }
    public decimal RestDoubleLegalOT { get; set; }

    public decimal RegularND { get; set; }
    public decimal RestDayND { get; set; }
    public decimal LegalHolND { get; set; }
    public decimal RestLegalDayND { get; set; }
    public decimal SpecialNonWorkingND { get; set; }
    public decimal RestSpecialDayND { get; set; }
    public decimal DoubleLegalND { get; set; }
    public decimal RestDoubleLegalND { get; set; }

    public decimal RegularNDOT { get; set; }
    public decimal RestDayNDOT { get; set; }
    public decimal LegalHolNDOT { get; set; }
    public decimal RestLegalDayNDOT { get; set; }
    public decimal SpecialNonWorkingNDOT { get; set; }
    public decimal RestSpecialDayNDOT { get; set; }
    public decimal DoubleLegalNDOT { get; set; }
    public decimal RestDoubleLegalNDOT { get; set; }

    public AbsentInfo AbsentInfo { get; set; } = new();
    //public List<LeaveInfo> Leaves { get; set; } = new();
    public LateInfo LateHourInfo { get; set; } = new();
    public UnderTimeInfo UTHourInfo { get; set; } = new();
    public OvertimeInfo OTHourInfo { get; set; } = new();
    public NightDiffInfo NightDiffInfo { get; set; } = new();
    public string Remarks { get; set; } = string.Empty;

}
