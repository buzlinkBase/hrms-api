using System.Linq.Expressions;
using Hrms.Domain.Entities.Announcements;
using Hrms.Domain.Entities.EmployeeEntities;

namespace Hrms.Core.Services.Announcements.Audience;

/// <summary>
/// Composes audience rules into one employee query: (any inclusion rule) AND NOT (any exclusion
/// rule), plus the active-employee filter. Each rule's own predicate comes from its
/// <see cref="IAudienceRuleStrategy"/>. The same query drives the composer's live preview and the
/// recipient snapshot taken at publish, so the two can never disagree.
/// </summary>
public class AudienceResolver
{
    // Employees who have left -- never part of an audience unless the publisher opts in.
    public static readonly EmploymentStatus[] InactiveStatuses =
    [
        EmploymentStatus.Terminated, EmploymentStatus.Resigned, EmploymentStatus.Retired, EmploymentStatus.Deceased,
    ];

    private readonly IReadOnlyDictionary<AudienceRuleKind, IAudienceRuleStrategy> _strategies;

    public AudienceResolver(IEnumerable<IAudienceRuleStrategy> strategies)
    {
        _strategies = strategies.ToDictionary(s => s.Kind);
    }

    public IQueryable<Employee> Resolve(IQueryable<Employee> employees, IReadOnlyCollection<AudienceRuleSpec> rules, bool excludeInactive)
    {
        var inclusions = rules.Where(r => !r.IsExclusion).Select(ToPredicate).ToList();
        // No inclusion rule means nobody -- never "everyone" by accident.
        if (inclusions.Count == 0) return employees.Where(e => false);

        var query = employees.Where(AnyOf(inclusions));

        var exclusions = rules.Where(r => r.IsExclusion).Select(ToPredicate).ToList();
        if (exclusions.Count > 0)
            query = query.Where(Not(AnyOf(exclusions)));

        if (excludeInactive)
            query = query.Where(e => !InactiveStatuses.Contains(e.EmploymentStatus));

        return query;
    }

    private Expression<Func<Employee, bool>> ToPredicate(AudienceRuleSpec rule) =>
        _strategies.TryGetValue(rule.Kind, out var strategy)
            ? strategy.ToPredicate(rule)
            : throw new ValidationException($"Unsupported audience rule \"{rule.Kind}\".");

    private static Expression<Func<Employee, bool>> AnyOf(IReadOnlyList<Expression<Func<Employee, bool>>> predicates)
    {
        var parameter = Expression.Parameter(typeof(Employee), "e");
        var body = predicates
            .Select(p => new ParameterReplacer(p.Parameters[0], parameter).Visit(p.Body)!)
            .Aggregate(Expression.OrElse);
        return Expression.Lambda<Func<Employee, bool>>(body, parameter);
    }

    private static Expression<Func<Employee, bool>> Not(Expression<Func<Employee, bool>> predicate) =>
        Expression.Lambda<Func<Employee, bool>>(Expression.Not(predicate.Body), predicate.Parameters);

    // Rebinds each rule's own lambda parameter to the shared one, so the OR-ed body is one
    // lambda EF can translate (Expression.Invoke would not be).
    private sealed class ParameterReplacer(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : base.VisitParameter(node);
    }
}
