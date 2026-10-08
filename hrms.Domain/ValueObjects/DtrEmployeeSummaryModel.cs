namespace Hrms.Domain.ValueObjects;

/// <summary>
/// One employee's calculated DTR for a cutoff, boiled down to what the Calculate DTR review table
/// shows. The day-level rows stay on the server until someone expands the employee, so a
/// 2,000-employee run reviews as ~2,000 small rows instead of tens of thousands of full ones.
/// </summary>
public class DtrEmployeeSummaryModel
{
    public Guid EmployeeId { get; set; }
    public string? FullName { get; set; }

    /// <summary>Distinct calculated days, ascending -- the UI subtracts the already-saved days from these.</summary>
    public List<DateOnly> WorkDates { get; set; } = [];

    public double RegularHours { get; set; }
    public double OtHours { get; set; }
    public double NdHours { get; set; }
    public double LateMinutes { get; set; }
    public double UtMinutes { get; set; }
    public int Absences { get; set; }
    public double LeaveHours { get; set; }

    /// <summary>Days with a time-in but no time-out, or the other way round.</summary>
    public int MissingPunchDays { get; set; }
}

/// <summary>
/// "Save Draft" by reference: the Calculate DTR screen sends what it generated from (the filter) and
/// which employees were ticked, and the server recomputes and saves the rows itself -- the browser
/// never uploads the calculated rows.
/// </summary>
public class SaveDtrDraftRequest
{
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public Guid? PayrollGroupId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? ClientId { get; set; }
    public Guid? OperationAreaId { get; set; }
    public List<Guid> EmployeeIds { get; set; } = [];
    public string? PostingDescription { get; set; }
}
