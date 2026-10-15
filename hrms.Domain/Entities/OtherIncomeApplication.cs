using BuzlinkRepository;
using System.Collections.ObjectModel;
namespace Hrms.Domain.Entities;

public class OtherIncomeApplication : BaseEntity, IEmployeeOwned, IEntryRecord
{
    public Guid EmployeeId { get; set; }
    public DateOnly EncodeDate { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public AllowanceFrequency FrequencyOfPayment { get; set; } = AllowanceFrequency.SemiMonthly;
    public Guid IncomeId { get; set; }
    public decimal Amount { get; set; }
    public string ProcessBy { get; set; }
    public virtual ICollection<OtherIncomeSchedules> Schedule { get; set; }
    // Retired: no payroll code ever read it and the UI no longer writes it. The column stays so
    // existing data isn't lost.
    public bool IsProrated { get; set; }
    public bool IsTaxable { get; set; }
    public string Remarks { get; set; }

    // --- How the income is given (FixedSchedule rows have none of the rules below) ---
    public OtherIncomePayMode PayMode { get; set; } = OtherIncomePayMode.FixedSchedule;
    public OtherIncomeAttendanceBasis AttendanceBasis { get; set; } = OtherIncomeAttendanceBasis.None;
    // The day must have at least this many worked hours. Null = no minimum.
    public decimal? MinHours { get; set; }
    // Late / undertime limits in minutes. 0 = must be punctual, null = not checked.
    public int? MaxLateMinutes { get; set; }
    public int? MaxUndertimeMinutes { get; set; }
    // OtherIncomeWeekdays / OtherIncomeDayTypes bit masks.
    public int ApplicableDays { get; set; } = (int)OtherIncomeWeekdays.All;
    public int DayTypes { get; set; } = (int)OtherIncomeDayTypes.All;
    // PeriodicGated only: qualifying days needed in a window. Null = every applicable day.
    public int? MinQualifyingDays { get; set; }

    public OtherIncomeApplication()
    {
        EncodeDate = DateOnly.FromDateTime(DateTime.Today);
        StartDate = DateOnly.MinValue;
        EndDate = DateOnly.MinValue;
        FrequencyOfPayment = AllowanceFrequency.SemiMonthly;
        Amount = 0;
        ProcessBy = "";
        Remarks = "";
        Schedule = new Collection<OtherIncomeSchedules>();
    }
}

// Hard-deleted: a row payroll never consumed has no history worth keeping, and one it did
// consume is locked (OtherIncomeScheduleReconciler) so it's never deleted at all.
[DisableSoftDelete]
public class OtherIncomeSchedules : BaseEntity, IEmployeeOwned, IEntryRecord
{
    public Guid EmployeeId { get; set; }
    // The single FK to the owning application (see OtherIncomeApplicationConfig). Set through the
    // Schedule navigation, never by hand -- the parent's Id is still Guid.Empty until EF adds it.
    public Guid OtherIncomeApplicationId { get; set; }
    public Guid IncomeId { get; set; }
    public virtual OtherIncome? Income { get; set; }
    public DateOnly Date { get; set; }
    public decimal Amount { get; set; }
    public bool IsTaxable { get; set; }
    public bool IsProrated { get; set; }
    public string Notes { get; set; } = string.Empty;
    // PeriodicGated rows: the attendance window this payment covers (inclusive).
    public DateOnly? PeriodStart { get; set; }
    public DateOnly? PeriodEnd { get; set; }
    // Set at payroll save when the attendance rule wasn't met; the row is still consumed so it
    // never carries over. SkipReason is an OtherIncomeSkipReasons code.
    public bool IsSkipped { get; set; }
    public string? SkipReason { get; set; }
    // Null = available to be picked up by a payroll run. Stamped with the Payroll.Id that
    // actually applied it once that run is SAVED (not merely calculated/previewed) — see
    // PayrollInputConsumptionService.MarkConsumedAsync. Nulled back out if that batch is
    // later deleted (PayrollBatchLifecycleService.DeleteBatchAsync), releasing the row for
    // the next run.
    public Guid? ConsumedByPayrollId { get; set; }
}

public class IncomePayment : BaseEntity, IEmployeeOwned, IEntryRecord
{
    public Guid EmployeeId { get; set; }
    public Guid IncomeId { get; set; }
    public DateOnly PayrollDate { get; set; }
    public decimal Amount { get; set; }
}