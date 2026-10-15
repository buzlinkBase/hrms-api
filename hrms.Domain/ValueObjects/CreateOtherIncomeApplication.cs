namespace Hrms.Domain.ValueObjects;

public abstract class OtherIncomeApplicationBase
{
    public Guid EmployeeId { get; set; }
    public DateOnly EncodeDate { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public AllowanceFrequency FrequencyOfPayment { get; set; } = AllowanceFrequency.SemiMonthly;
    public Guid IncomeId { get; set; }
    public bool IsTaxable { get; set; }
    public decimal Amount { get; set; }
    public string Remarks { get; set; } = string.Empty;

    public OtherIncomePayMode PayMode { get; set; } = OtherIncomePayMode.FixedSchedule;
    public OtherIncomeAttendanceBasis AttendanceBasis { get; set; } = OtherIncomeAttendanceBasis.None;
    public decimal? MinHours { get; set; }
    public int? MaxLateMinutes { get; set; }
    public int? MaxUndertimeMinutes { get; set; }
    public int ApplicableDays { get; set; } = (int)OtherIncomeWeekdays.All;
    public int DayTypes { get; set; } = (int)OtherIncomeDayTypes.All;
    public int? MinQualifyingDays { get; set; }
}
public class CreateOtherIncomeApplication : OtherIncomeApplicationBase
{
    public virtual ICollection<CreateOtherIncomeSchedule> Schedule { get; set; }
}

public class UpdateOtherIncomeApplication : OtherIncomeApplicationBase
{
    public Guid Id { get; set; }
    public virtual ICollection<UpdateOtherIncomeSchedule> Schedule { get; set; }

}

public class OtherIncomeApplicationModel : UpdateOtherIncomeApplication { }

public class CreateOtherIncomeSchedule
{
    public DateOnly Date { get; set; }
    public decimal Amount { get; set; }
    public DateOnly? PeriodStart { get; set; }
    public DateOnly? PeriodEnd { get; set; }
}

public class UpdateOtherIncomeSchedule : CreateOtherIncomeSchedule
{
    public Guid Id { get; set; }
    // Read-only for the client: set by payroll, echoed back so the detail view can show status.
    public bool IsSkipped { get; set; }
    public string? SkipReason { get; set; }
    public Guid? ConsumedByPayrollId { get; set; }
}