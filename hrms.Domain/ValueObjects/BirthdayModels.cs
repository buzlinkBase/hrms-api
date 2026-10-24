namespace Hrms.Domain.ValueObjects;

/// <summary>A coworker whose birthday is today or coming up this week and who opted in to be
/// greeted. Deliberately carries no birth year, age or date of birth -- only how many days away it
/// is (0 = today).</summary>
public class CoworkerBirthdayModel
{
    public Guid Id { get; set; }
    public int DaysUntil { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string ProfileImg { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public string PositionName { get; set; } = string.Empty;
}

/// <summary>Admin Dashboard row: every active employee with a birthday coming up, whether or not
/// they opted in to public greetings.</summary>
public class UpcomingBirthdayModel
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public int Month { get; set; }
    public int Day { get; set; }
    public int DaysUntil { get; set; }
    public bool ShareBirthday { get; set; }
}
