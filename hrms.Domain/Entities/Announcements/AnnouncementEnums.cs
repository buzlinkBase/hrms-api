namespace Hrms.Domain.Entities.Announcements;

// Lifecycle of a company announcement -- see AnnouncementStateMachine (Hrms.Core) for which
// actions each state allows. Deleted is the "Recently deleted" bin for announcements that were
// already sent (a never-sent Draft/Scheduled one is removed outright instead).
public enum AnnouncementState
{
    Draft,
    Scheduled,
    Published,
    Unpublished,
    Archived,
    Deleted,
}

public enum AnnouncementPriority
{
    Normal,
    Important,
    Urgent,
}

// Who an announcement goes to. Rules are OR-ed together; an exclusion rule removes its matches
// from that union (see AudienceResolver).
public enum AudienceRuleKind
{
    AllEmployees,
    Department,
    Position,
    Branch,
    ProjectSite,
    Client,
    Section,
    EmploymentStatus,
    Employee,
}
