using System.Linq.Expressions;
using Hrms.Domain.Entities.Announcements;
using Hrms.Domain.Entities.EmployeeEntities;

namespace Hrms.Core.Services.Announcements.Audience;

public sealed record AudienceRuleSpec(AudienceRuleKind Kind, Guid? TargetId, string? TargetValue, bool IsExclusion);

/// <summary>
/// Strategy for one kind of audience rule: turns it into an employee predicate EF can translate.
/// Adding a new way to target people is a new class registered in DI -- AudienceResolver itself
/// never changes (open/closed).
/// </summary>
public interface IAudienceRuleStrategy
{
    AudienceRuleKind Kind { get; }
    Expression<Func<Employee, bool>> ToPredicate(AudienceRuleSpec rule);
}

public sealed class AllEmployeesAudienceRule : IAudienceRuleStrategy
{
    public AudienceRuleKind Kind => AudienceRuleKind.AllEmployees;
    public Expression<Func<Employee, bool>> ToPredicate(AudienceRuleSpec rule) => e => true;
}

// Every rule keyed on one of Employee's organisational ids shares this shape.
public abstract class EmployeeIdAudienceRule : IAudienceRuleStrategy
{
    public abstract AudienceRuleKind Kind { get; }

    public Expression<Func<Employee, bool>> ToPredicate(AudienceRuleSpec rule)
    {
        var targetId = rule.TargetId ?? throw new ValidationException($"A {Kind} audience rule needs a target.");
        return Match(targetId);
    }

    protected abstract Expression<Func<Employee, bool>> Match(Guid targetId);
}

public sealed class DepartmentAudienceRule : EmployeeIdAudienceRule
{
    public override AudienceRuleKind Kind => AudienceRuleKind.Department;
    protected override Expression<Func<Employee, bool>> Match(Guid id) => e => e.DepartmentId == id;
}

public sealed class PositionAudienceRule : EmployeeIdAudienceRule
{
    public override AudienceRuleKind Kind => AudienceRuleKind.Position;
    protected override Expression<Func<Employee, bool>> Match(Guid id) => e => e.PositionId == id;
}

public sealed class BranchAudienceRule : EmployeeIdAudienceRule
{
    public override AudienceRuleKind Kind => AudienceRuleKind.Branch;
    protected override Expression<Func<Employee, bool>> Match(Guid id) => e => e.BranchId == id;
}

public sealed class ProjectSiteAudienceRule : EmployeeIdAudienceRule
{
    public override AudienceRuleKind Kind => AudienceRuleKind.ProjectSite;
    protected override Expression<Func<Employee, bool>> Match(Guid id) => e => e.AreaId == id;
}

public sealed class ClientAudienceRule : EmployeeIdAudienceRule
{
    public override AudienceRuleKind Kind => AudienceRuleKind.Client;
    protected override Expression<Func<Employee, bool>> Match(Guid id) => e => e.ClientId == id;
}

public sealed class SectionAudienceRule : EmployeeIdAudienceRule
{
    public override AudienceRuleKind Kind => AudienceRuleKind.Section;
    protected override Expression<Func<Employee, bool>> Match(Guid id) => e => e.SectionId == id;
}

public sealed class SpecificEmployeeAudienceRule : EmployeeIdAudienceRule
{
    public override AudienceRuleKind Kind => AudienceRuleKind.Employee;
    protected override Expression<Func<Employee, bool>> Match(Guid id) => e => e.Id == id;
}

public sealed class EmploymentStatusAudienceRule : IAudienceRuleStrategy
{
    public AudienceRuleKind Kind => AudienceRuleKind.EmploymentStatus;

    public Expression<Func<Employee, bool>> ToPredicate(AudienceRuleSpec rule)
    {
        if (!Enum.TryParse<EmploymentStatus>(rule.TargetValue, ignoreCase: true, out var status))
            throw new ValidationException($"\"{rule.TargetValue}\" is not an employment status.");
        return e => e.EmploymentStatus == status;
    }
}
