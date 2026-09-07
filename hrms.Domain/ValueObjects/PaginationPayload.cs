namespace Hrms.Domain.ValueObjects;

public record DateRangePayload(DateOnly FromDate, DateOnly ToDate);
public record PaginationPayload(string? Keyword, int Page, int? Limit);
public record DTRQueryPayload(DateOnly FromDate, DateOnly ToDate, Guid? DepartmentId, Guid? EmployeeId, Guid? ClientId, Guid? PayrollGroupId);
public record PayrollCalcPayload(DateOnly FromDate, DateOnly ToDate, Guid? DepartmentId, Guid? EmployeeId, Guid? ClientId, Guid? PayrollGroupId) :
    DTRQueryPayload(FromDate, ToDate, DepartmentId, EmployeeId, ClientId, PayrollGroupId)
{
    public List<string>? BatchCodes { get; init; }
}

// Payload for the UI-driven payroll run — only the selected posted batch codes.
// Date range and employees are derived from the actual DTR records in those batches.
public record PayrollRunPayload(List<string> BatchCodes)
{
    // Explicit Pay/Release Date, required only when CrossMonthStatutoryCreditPolicy or
    // WTaxCrossMonthCreditPolicy is set to PayDate — see PayrollProcessorService.CalculateAsync.
    public DateOnly? PayDate { get; init; }
    // Free-text identity for this run — captured once at Generate time (see the frontend's
    // Generate Payroll confirmation dialog) and stamped onto every Payroll row it produces,
    // so Post/Delete Payroll Run can show which run is which.
    public string? Remarks { get; init; }
}

// Payload for the "Generate 13th Month Pay" run — a lump-sum, non-attendance-based payout,
// so unlike PayrollRunPayload there's no DTR batch to select from. PayrollGroupIds/EmployeeIds
// scope which employees are included; both null/empty means everyone with posted regular pay
// in Year. See PayrollProcessorService.GenerateThirteenthMonthAsync.
public record ThirteenthMonthRunPayload(int Year)
{
    public List<Guid>? PayrollGroupIds { get; init; }
    public List<Guid>? EmployeeIds { get; init; }
    public DateOnly? PayDate { get; init; }
    public string? Remarks { get; init; }
}
// EmployeeIds is required (not optional like ThirteenthMonthRunPayload's) — Last Pay is
// never run "for everyone", only for specific separated employees being settled. Each
// employee's own DateResigned anchors their proration window, so no Year/PayrollGroupIds
// scoping is needed here. See PayrollProcessorService.GenerateLastPayAsync.
public record LastPayRunPayload(List<Guid> EmployeeIds)
{
    public DateOnly? PayDate { get; init; }
    public string? Remarks { get; init; }
    // Selectable components — each defaults to today's existing behavior (always included) so
    // an existing caller that doesn't set these keeps working unchanged.
    public bool IncludeThirteenthMonth { get; init; } = true;
    public bool IncludeLeaveConversion { get; init; } = true;
    // Explicit opt-in only — omitted/empty means none applied. Sourced from
    // LastPayrollService.GetAvailableSalaryAdjustmentsAsync/GetAvailableOtherIncomeAsync and
    // confirmed by HR before Generate is called. See PayrollInputConsumptionService for how
    // these get marked consumed once this run is saved.
    public List<Guid>? SalaryAdjustmentIds { get; init; }
    public List<Guid>? OtherIncomeScheduleIds { get; init; }
}
public class CompanyPolicyRule
{
    public OvertimeInclusionPolicy OTInclusionPolicy { get; set; }
    public OvertimeEligibilityRule OTEligibility { get; set; }
    public bool ApplyStatutoryOnActualMonth { get; set; }
    public CrossMonthStatutoryCreditPolicy CrossMonthStatutoryCreditPolicy { get; set; } = CrossMonthStatutoryCreditPolicy.CutoffStartMonth;
    public CrossMonthStatutoryCreditPolicy WTaxCrossMonthCreditPolicy { get; set; } = CrossMonthStatutoryCreditPolicy.CutoffEndMonth;
    public decimal RequiredTakehomePercentage { get; set; } = 10;
    //public int RequiredWorkingDays { get; set; } = 22;
    //public int TotalDaysInaYear { get; set; } = 264;//22*12 use for daily rate computation for fix rate
    //public decimal StatutoryCap { get; set; }
    //public decimal _13thMonthCap { get; set; } = 90_000;
}


public class PayrollGroupQuery
{
    public RecordStatus? Status { get; set; } = RecordStatus.Any;
}