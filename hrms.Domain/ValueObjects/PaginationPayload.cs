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
public record PayrollRunPayload(List<string> BatchCodes);
public class CompanyPolicyRule
{
    public OvertimeInclusionPolicy OTInclusionPolicy { get; set; }
    public OvertimeEligibilityRule OTEligibility { get; set; }
    public bool ApplyStatutoryOnActualMonth { get; set; }
    public decimal RequiredTakehomePercentage { get; set; } = 10;
    public int RequiredWorkingDays { get; set; } = 22;
    public int TotalDaysInaYear { get; set; } = 264;//22*12 use for daily rate computation for fix rate
    //public decimal StatutoryCap { get; set; }
    //public decimal _13thMonthCap { get; set; } = 90_000;
}


public class PayrollGroupQuery
{
    public RecordStatus? Status { get; set; } = RecordStatus.Any;
}