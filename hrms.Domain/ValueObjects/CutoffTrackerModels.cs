namespace Hrms.Domain.ValueObjects;

// Cutoff Tracker: for one cutoff, every employee who worked for a client, with where their days
// went and which run (if any) carries their deductions. See CutoffTrackerService.
public class CutoffTrackerResult
{
    // The company's deduction-carrier rule (DeductionCarrierMode). EveryRun means nothing is gated,
    // so there are no carriers to track and the screen says so.
    public string Mode { get; set; } = nameof(DeductionCarrierMode.EveryRun);
    public List<CutoffTrackerEmployee> Employees { get; set; } = [];
}

public class CutoffTrackerEmployee
{
    public Guid EmployeeId { get; set; }
    public string? EmployeeNo { get; set; }
    public string? FullName { get; set; }
    // The client on the employee's own record -- what the PrimaryClient rule ranks first.
    public Guid? HomeClientId { get; set; }
    public List<CutoffTrackerClientDays> Clients { get; set; } = [];
    // The run that carries this cutoff's deductions; null = no run has taken them yet.
    public CutoffTrackerCarrier? Carrier { get; set; }
}

public class CutoffTrackerClientDays
{
    // Null = days with no client stamped.
    public Guid? ClientId { get; set; }
    // Days actually worked (any DTR status).
    public int WorkedDays { get; set; }
    // Approved days no run has paid yet.
    public int UnpaidApprovedDays { get; set; }
    // Days already paid by a saved run.
    public int PaidDays { get; set; }
}

public class CutoffTrackerCarrier
{
    public Guid PayrollBatchId { get; set; }
    public Guid? ClientId { get; set; }
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public DateOnly? PayDate { get; set; }
    public string? Remarks { get; set; }
}
