using System.Linq.Expressions;
using BuzlinkRepository;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.Approvals;
using Hrms.Domain.Entities.EmployeeEntities;
using Microsoft.EntityFrameworkCore;

namespace Hrms.Infrastructure;

/// <summary>
/// What the signed-in caller may see of confidential employees, for this request. Filled once per
/// request (hrms-api ConfidentialAccessMiddleware); read by the global query filter at query time.
/// A context with no access object (background jobs, message consumers, tests) is unrestricted.
/// </summary>
public interface IConfidentialAccess
{
    /// <summary>False for cleared callers, the Owner, and anything with no signed-in user.</summary>
    bool Restricted { get; }

    /// <summary>
    /// Confidential employees whose records of this class the caller may not see (never includes
    /// their own record). A partial clearance (Operations / Entries) empties its own class.
    /// </summary>
    Guid[] HiddenEmployeeIdsFor(ConfidentialDataClass dataClass);

    /// <summary>
    /// Applications (and their approval instances) of hidden employees that this caller is the
    /// assigned approver of -- visible even though the employee is hidden.
    /// </summary>
    Guid[] ApprovalVisibleRecordIds { get; }

    /// <summary>The applicants of those applications: visible only so the request can show who filed it.</summary>
    Guid[] ApprovalVisibleEmployeeIds { get; }
}

/// <summary>A DbContext whose query filters read the caller's confidential access at query time.</summary>
public interface IConfidentialityContext
{
    bool RestrictConfidential { get; }
    Guid[] ConfidentialHiddenProfileIds { get; }
    Guid[] ConfidentialHiddenOperationsIds { get; }
    Guid[] ConfidentialHiddenEntriesIds { get; }
    Guid[] ConfidentialHiddenCompensationIds { get; }
    Guid[] ConfidentialApprovalRecordIds { get; }
    Guid[] ConfidentialApprovalEmployeeIds { get; }
}

/// <summary>
/// The confidential-employee rules of the global query filter -- the "domain level" access
/// restriction. Each rule is a small expression builder in Specification style (a criteria per
/// kind of record), composed into the same per-entity filter as the tenant/soft-delete condition
/// (see TenantQueryFilters). Because it's a global filter, EF applies it to every query,
/// navigation and Include -- no screen has to remember to.
///
/// Every rule starts with "not restricted OR ...", so cleared callers and background work pay
/// only one bound parameter.
/// </summary>
public static class ConfidentialityFilters
{
    private static readonly System.Reflection.MethodInfo ContainsGuid =
        typeof(Enumerable).GetMethods()
            .Single(m => m.Name == nameof(Enumerable.Contains) && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(Guid));

    /// <summary>
    /// Every employee of the current company, confidential ones included -- for integrity checks
    /// only (Bio ID / email uniqueness, import duplicate matching), where missing a hidden
    /// employee would let a duplicate through. Callers must not return these rows to the user.
    /// </summary>
    public static IQueryable<Employee> EmployeesForIntegrityChecks(this HrmsContext db) =>
        db.Employees.IgnoreQueryFilters()
            .Where(e => e.DeletedAt == null && (db.CurrentTenantId == System.Guid.Empty || e.TenantId == db.CurrentTenantId));

    /// <summary>
    /// Tenant + soft-delete only, without the confidentiality rule -- for the rare record of a hidden
    /// class that a visible purpose needs (e.g. the cash bond deducted on a payroll row, which is an
    /// Entries figure). Callers must narrow it to the employees and columns they may show.
    /// </summary>
    public static IQueryable<T> IgnoringConfidentiality<T>(this HrmsContext db)
        where T : class, IEntityTenant, ITimeStamp =>
        db.Set<T>().IgnoreQueryFilters()
            .Where(e => e.DeletedAt == null && (db.CurrentTenantId == System.Guid.Empty || e.TenantId == db.CurrentTenantId));

    /// <summary>The confidentiality condition for this entity type, or null when it doesn't apply.</summary>
    public static Expression? For<TContext>(Type entityType, ParameterExpression x, TContext context)
        where TContext : IConfidentialityContext
    {
        var ctx = Expression.Constant(context, typeof(TContext));
        var rules = new Rules(ctx, x);

        if (entityType == typeof(Employee)) return rules.EmployeeAccessRule();
        if (entityType == typeof(ApprovalInstance)) return rules.ApprovalInstanceAccessRule();
        var hiddenList = HiddenListFor(ConfidentialDataClasses.Of(entityType));
        if (typeof(IEmployeeOwned).IsAssignableFrom(entityType))
            return rules.EmployeeOwnedAccessRule(hiddenList, typeof(IApprovalSubject).IsAssignableFrom(entityType));
        if (typeof(IOptionalEmployeeOwned).IsAssignableFrom(entityType))
            return rules.OptionalEmployeeOwnedAccessRule(hiddenList);
        return null;
    }

    // The context property holding the hidden employees for a class of record.
    private static string HiddenListFor(ConfidentialDataClass dataClass) => dataClass switch
    {
        ConfidentialDataClass.Profile => nameof(IConfidentialityContext.ConfidentialHiddenProfileIds),
        ConfidentialDataClass.Entries => nameof(IConfidentialityContext.ConfidentialHiddenEntriesIds),
        ConfidentialDataClass.Compensation => nameof(IConfidentialityContext.ConfidentialHiddenCompensationIds),
        _ => nameof(IConfidentialityContext.ConfidentialHiddenOperationsIds),
    };

    private sealed class Rules(ConstantExpression ctx, ParameterExpression x)
    {
        private Expression NotRestricted =>
            Expression.Not(Expression.Property(ctx, nameof(IConfidentialityContext.RestrictConfidential)));

        private Expression In(string listProperty, Expression id) =>
            Expression.Call(ContainsGuid, Expression.Property(ctx, listProperty), id);

        private Expression HiddenProfile(Expression id) =>
            In(nameof(IConfidentialityContext.ConfidentialHiddenProfileIds), id);

        private Expression ApprovalRecord(Expression id) =>
            In(nameof(IConfidentialityContext.ConfidentialApprovalRecordIds), id);

        private Expression Guid(string property) =>
            Expression.Call(typeof(EF), nameof(EF.Property), [typeof(Guid)], x, Expression.Constant(property));

        // The employee themself: hidden unless they filed a request this caller is approving.
        public Expression EmployeeAccessRule()
        {
            var id = Guid(nameof(Employee.Id));
            return Expression.OrElse(NotRestricted, Expression.OrElse(
                Expression.Not(HiddenProfile(id)),
                In(nameof(IConfidentialityContext.ConfidentialApprovalEmployeeIds), id)));
        }

        // Anything the employee owns (payroll, DTR, applications, ledgers, 201 file ...), checked
        // against the hidden list of its class. An application being approved by this caller stays
        // visible on its own.
        public Expression EmployeeOwnedAccessRule(string hiddenList, bool isApprovalSubject)
        {
            Expression allowed = Expression.Not(In(hiddenList, Guid(nameof(IEmployeeOwned.EmployeeId))));
            if (isApprovalSubject)
                allowed = Expression.OrElse(allowed, ApprovalRecord(Guid(nameof(IApprovalSubject.Id))));
            return Expression.OrElse(NotRestricted, allowed);
        }

        public Expression OptionalEmployeeOwnedAccessRule(string hiddenList)
        {
            var employeeId = Expression.Call(typeof(EF), nameof(EF.Property), [typeof(Guid?)], x,
                Expression.Constant(nameof(IOptionalEmployeeOwned.EmployeeId)));
            return Expression.OrElse(NotRestricted, Expression.OrElse(
                Expression.Equal(employeeId, Expression.Constant(null, typeof(Guid?))),
                Expression.Not(In(hiddenList, Expression.Property(employeeId, nameof(Nullable<Guid>.Value))))));
        }

        // An approval instance follows its applicant, except the ones this caller is approving.
        public Expression ApprovalInstanceAccessRule() =>
            Expression.OrElse(NotRestricted, Expression.OrElse(
                Expression.Not(HiddenProfile(Guid(nameof(ApprovalInstance.ApplicantEmployeeId)))),
                ApprovalRecord(Guid(nameof(ApprovalInstance.Id)))));
    }
}
