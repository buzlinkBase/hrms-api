namespace Hrms.Domain.ValueObjects;

// Slim, coworker-safe views of an employee: never pay, DOB, age or government ids.
public class CoworkerCardModel
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string ProfileImg { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public string PositionName { get; set; } = string.Empty;
}

public class AnniversaryModel : CoworkerCardModel
{
    public int Years { get; set; }
    public int DaysUntil { get; set; }
}

/// <summary>Someone away today. <see cref="Note"/> is only "Out", "Half day" or "Away" -- never the
/// leave type, reason or destination.</summary>
public class OutTodayModel : CoworkerCardModel
{
    public string Note { get; set; } = string.Empty;
}

/// <summary>Email and contact are filled only when the employee opted in (ShareContact).</summary>
public class DirectoryEntryModel : CoworkerCardModel
{
    public string BranchName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Contact { get; set; }
}

public class DirectoryPageModel
{
    public List<DirectoryEntryModel> Items { get; set; } = [];
    public int Total { get; set; }
}

public static class OrgNodeKinds
{
    public const string Company = "company";
    public const string Department = "department";
    public const string Section = "section";
    public const string Employee = "employee";
}

// One box in the org chart: the company, a department, a section or a person. Units carry the
// number of people beneath them; people carry their position and its level.
public class OrgNodeModel
{
    public string Id { get; set; } = string.Empty;
    public string Kind { get; set; } = OrgNodeKinds.Employee;
    public string Name { get; set; } = string.Empty;
    public string PositionName { get; set; } = string.Empty;
    public int Level { get; set; }
    public int HeadCount { get; set; }
    public List<OrgNodeModel> Children { get; set; } = [];
}

public class CoworkerSettingsModel
{
    public bool ShowNewHires { get; set; }
    public bool ShowAnniversaries { get; set; }
    public bool ShowWhosOut { get; set; }
    public bool ShowDirectory { get; set; }
}
