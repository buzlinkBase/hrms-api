using Onepunch.AuditEngine;

namespace Hrms.Domain.Entities;

/// <summary>
/// A place where the company serves a client: one of the client's sites, tied to a project site (operation area) for attendance and
/// payroll. A client can have several sites, and one project site can host more than one client. <see cref="SubAreaLabel"/> names a
/// part of the site billed on its own ("AGWA").
/// </summary>
[Audited]
public class ClientSite : BaseEntity
{
    public Guid ClientId { get; set; }
    public Guid AreaId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? SubAreaLabel { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
}

/// <summary>
/// A post (station) at a client site: a guard house, a gate, a roving beat, a K9 team. <see cref="Position"/> is the kind of personnel the
/// post needs ("Regular guard", "Detachment commander", "Lady guard"); it is free text so other industries can use their own roles.
/// </summary>
[Audited]
public class ClientPost : BaseEntity
{
    public Guid ClientSiteId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public bool Armed { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// The contracted manning of a post: how many people it needs on a shift, on which days, from a date. A later row for the same post and
/// shift takes over from its start date (the earlier one is closed the day before), so past periods keep the manning they had.
/// </summary>
[Audited]
public class PostRequirement : BaseEntity
{
    public Guid PostId { get; set; }

    /// <summary>The shift as people say it ("DS", "NS", "Day", "Night") — it matches the shift name on time logs.</summary>
    public string ShiftLabel { get; set; } = string.Empty;

    /// <summary>The company's time shift for the post, when one fits; null for posts whose hours vary.</summary>
    public Guid? TimeShiftId { get; set; }

    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public int RequiredHeadcount { get; set; }

    /// <summary>Days the post is manned: a comma list of Mon,Tue,Wed,Thu,Fri,Sat,Sun; empty = every day.</summary>
    public string DaysOfWeek { get; set; } = string.Empty;

    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
}

public enum DeploymentRole
{
    /// <summary>The post's own guard. One open regular deployment per person; assigning another one ends the old one.</summary>
    Regular = 0,

    /// <summary>Covers a regular guard's day off, leave or absence.</summary>
    Reliever = 1,

    /// <summary>Extra personnel for an event or a short-staffed period, billed on their own.</summary>
    Augmentation = 2,
}

/// <summary>
/// Who is (or was) deployed to a post, in what role, from when to when — the deployment history behind manning, billing and the client
/// portal. A person's current regular deployment also sets their client and project site on the employee record.
/// </summary>
[Audited]
public class Deployment : BaseEntity
{
    public Guid EmployeeId { get; set; }
    public Guid PostId { get; set; }
    public DeploymentRole Role { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? Reason { get; set; }

    /// <summary>For augmentation: the event or requesting party it was for ("CWE 2026 - PGB").</summary>
    public string? EventTag { get; set; }
}
