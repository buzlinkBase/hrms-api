using Hrms.Domain.Entities;

namespace Hrms.Domain.ValueObjects;

public record ClientSiteRequest(string Code, string Name, Guid AreaId, string? SubAreaLabel, DateOnly? StartDate, DateOnly? EndDate);

public record ClientSiteModel(
    Guid Id, Guid ClientId, string Code, string Name, Guid AreaId, string AreaName, string? SubAreaLabel, DateOnly? StartDate, DateOnly? EndDate,
    int PostCount, int RequiredHeadcount, int DeployedHeadcount);

public record ClientPostRequest(string Code, string Name, string Position, bool Armed, string? Notes);

public record PostRequirementRequest(
    string ShiftLabel, Guid? TimeShiftId, TimeOnly? StartTime, TimeOnly? EndTime, int RequiredHeadcount, IReadOnlyList<string>? DaysOfWeek,
    DateOnly EffectiveFrom, DateOnly? EffectiveTo);

public record PostRequirementModel(
    Guid Id, Guid PostId, string ShiftLabel, Guid? TimeShiftId, TimeOnly? StartTime, TimeOnly? EndTime, int RequiredHeadcount,
    IReadOnlyList<string> DaysOfWeek, DateOnly EffectiveFrom, DateOnly? EffectiveTo);

public record ClientPostModel(
    Guid Id, Guid ClientSiteId, string Code, string Name, string Position, bool Armed, string? Notes,
    IReadOnlyList<PostRequirementModel> CurrentRequirements, IReadOnlyList<PostRequirementModel> Requirements, int DeployedRegulars);

public record AssignDeploymentRequest(
    Guid EmployeeId, Guid PostId, DeploymentRole Role, DateOnly StartDate, DateOnly? EndDate, string? Reason, string? EventTag);

public record EndDeploymentRequest(DateOnly EndDate, string? Reason);

public record DeploymentModel(
    Guid Id, Guid EmployeeId, string EmployeeNo, string EmployeeName, Guid PostId, string PostName, Guid ClientSiteId, string SiteName, Guid ClientId,
    string ClientName, DeploymentRole Role, DateOnly StartDate, DateOnly? EndDate, string? Reason, string? EventTag, bool Active);

public record DeploymentQuery(Guid? ClientId, Guid? SiteId, Guid? PostId, Guid? EmployeeId, DateOnly? ActiveOn);

/// <summary>
/// A post on a day: the people its shifts need (summed; <see cref="Shifts"/> breaks it down, e.g. "DS 1 · NS 1") against the people deployed
/// to it. Deployments are to a post, not a shift (who works which shift is the work schedule's business), so cover is counted per post.
/// </summary>
public record CoverageRow(
    Guid SiteId, string SiteName, Guid PostId, string PostName, string Position, string Shifts, int Required, int Regulars, int Relievers,
    int Augmentation, int Gap);

public record CoverageReport(Guid ClientId, DateOnly Date, int Required, int Deployed, int Gap, IReadOnlyList<CoverageRow> Rows);
