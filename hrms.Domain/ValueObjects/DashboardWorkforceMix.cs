namespace Hrms.Domain.ValueObjects;

// The Dashboard's "who works here right now" charts: current employees (not resigned) split by
// status, branch, project site and tenure. See DashboardWorkforceService.
public class DashboardWorkforceMix
{
    public List<DashboardCount> EmploymentStatus { get; set; } = [];
    public List<DashboardCount> Branches { get; set; } = [];
    public List<DashboardCount> Sites { get; set; } = [];
    public List<DashboardCount> Tenure { get; set; } = [];
}

public class DashboardCount
{
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
}
