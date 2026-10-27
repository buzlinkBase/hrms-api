using Hrms.Core.Services.Approvals;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.Approvals;
using Hrms.Domain.Entities.EmployeeEntities;

namespace Hrms.Core.Services.Security;

/// <summary>
/// The partial clearances an uncleared caller may hold for confidential employees (tenantstore
/// "Confidential Records" row). Full clearance ("Access") never gets this far -- see
/// <see cref="ConfidentialAccessScope"/>.
/// </summary>
/// <param name="Operations">DTR, attendance, schedules, applications, 201 details.</param>
/// <param name="Entries">Cash bond, allowances / other income, deductions and loans.</param>
public readonly record struct ConfidentialGrants(bool Operations, bool Entries)
{
    public static readonly ConfidentialGrants None = new(false, false);
    public bool Any => Operations || Entries;

    /// <summary>
    /// Only the grant that covers this class of data -- what an endpoint working on it may use
    /// (hrms-api ConfidentialDataAttribute). Pay figures are never covered by a partial grant.
    /// </summary>
    public ConfidentialGrants NarrowTo(ConfidentialDataClass dataClass) => dataClass switch
    {
        ConfidentialDataClass.Operations => new(Operations, false),
        ConfidentialDataClass.Entries => new(false, Entries),
        ConfidentialDataClass.Compensation => None,
        _ => this,
    };
}

/// <summary>
/// What this request's caller may see of confidential employees (<see cref="IConfidentialAccess"/>,
/// read by the global query filter). Scoped per request and filled once, before any controller
/// query, by hrms-api's ConfidentialAccessMiddleware -- only for a signed-in caller without full
/// clearance. Left untouched (unrestricted) for cleared callers, the Owner, and everything with no
/// signed-in user: message consumers, background jobs, tests.
/// </summary>
public sealed class ConfidentialAccessScope : IConfidentialAccess
{
    /// <summary>Full clearance. Deliberately not satisfied by the Admin role on its own.</summary>
    public const string ClearancePermission = "Confidential Records:Access";
    public const string OperationsPermission = "Confidential Records:Operations";
    public const string EntriesPermission = "Confidential Records:Entries";

    // Confidential employees other than the caller -- hidden in every class the caller isn't
    // cleared for. Compensation is always among them.
    private Guid[] _confidential = [];

    public bool Restricted { get; private set; }
    public ConfidentialGrants Grants { get; private set; } = ConfidentialGrants.None;
    public Guid[] ApprovalVisibleRecordIds { get; private set; } = [];
    public Guid[] ApprovalVisibleEmployeeIds { get; private set; } = [];

    public Guid[] HiddenEmployeeIdsFor(ConfidentialDataClass dataClass)
    {
        if (!Restricted) return [];
        var cleared = dataClass switch
        {
            ConfidentialDataClass.Profile => Grants.Any,
            ConfidentialDataClass.Operations => Grants.Operations,
            ConfidentialDataClass.Entries => Grants.Entries,
            _ => false,
        };
        return cleared ? [] : _confidential;
    }

    /// <summary>
    /// How many confidential employees are missing from pay figures and reports for this caller --
    /// for the report notice. Every partial clearance still excludes them there.
    /// </summary>
    public int HiddenEmployeeCount => Restricted ? _confidential.Length : 0;

    public bool CanSee(ConfidentialDataClass dataClass, Guid employeeId) =>
        !HiddenEmployeeIdsFor(dataClass).Contains(employeeId);

    public bool CanSeeEmployee(Guid employeeId) => CanSee(ConfidentialDataClass.Profile, employeeId);

    /// <summary>
    /// Visible to this caller only because they're approving one of the employee's requests --
    /// employee screens (list, 201 file) must still leave them out.
    /// </summary>
    public bool IsApprovalOnlyEmployee(Guid employeeId) =>
        !CanSeeEmployee(employeeId) && ApprovalVisibleEmployeeIds.Contains(employeeId);

    /// <summary>
    /// Works out the hidden sets for a caller without full clearance. Runs with the query filters
    /// off and an explicit company condition, since it is what the filters depend on. Costs one
    /// indexed query when the company has no confidential employees -- the common case.
    /// </summary>
    public async Task InitializeAsync(HrmsContext db, Guid tenantId, Guid userId, string? email,
        ConfidentialGrants grants, CancellationToken token)
    {
        var confidential = await db.Employees.IgnoreQueryFilters().AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.DeletedAt == null && e.IsConfidential)
            .Select(e => new { e.Id, e.UserId, e.Email })
            .ToListAsync(token);
        if (confidential.Count == 0) return;

        // A confidential employee always sees their own records (My Portal).
        var normalizedEmail = email?.Trim().ToLowerInvariant();
        var own = confidential.FirstOrDefault(e => e.UserId == userId)
                  ?? confidential.FirstOrDefault(e =>
                      e.UserId == null && normalizedEmail != null && e.Email?.Trim().ToLowerInvariant() == normalizedEmail);
        var others = confidential.Where(e => e != own).Select(e => e.Id).ToArray();
        if (others.Length == 0) return;

        Restricted = true;
        Grants = grants;
        _confidential = others;
        await AddApprovalExceptionsAsync(db, tenantId, userId, normalizedEmail, token);
    }

    // An approver assigned to (or who already acted on) a confidential employee's request sees that
    // one request, whatever its class. A no-workflow fallback step has no specific approver --
    // anyone with the coarse Approve permission -- so it grants no exception.
    private async Task AddApprovalExceptionsAsync(HrmsContext db, Guid tenantId, Guid userId, string? email, CancellationToken token)
    {
        var caller = await db.Employees.IgnoreQueryFilters().AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.DeletedAt == null &&
                        (e.UserId == userId || (email != null && e.Email != null && e.Email.ToLower() == email)))
            .OrderByDescending(e => e.UserId == userId)
            .FirstOrDefaultAsync(token);
        if (caller == null) return;

        var hidden = _confidential;
        var instances = await db.ApprovalInstances.IgnoreQueryFilters().AsNoTracking()
            .Where(i => i.TenantId == tenantId && i.DeletedAt == null && hidden.Contains(i.ApplicantEmployeeId))
            .Include(i => i.Actions)
            .Include(i => i.Workflow).ThenInclude(w => w!.Steps).ThenInclude(s => s.NamedApprovers)
            .Include(i => i.Workflow).ThenInclude(w => w!.Steps).ThenInclude(s => s.Groups)
            .ToListAsync(token);
        if (instances.Count == 0) return;

        var applicants = await db.Employees.IgnoreQueryFilters().AsNoTracking()
            .Where(e => hidden.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, token);

        var resolver = new ApproverEligibilityResolver();
        var approvers = new ApprovalApproverResolver(new DbContextOrgLookup(db));
        var delegatorsByType = await LoadDelegatorsAsync(db, tenantId, caller.Id, instances.Select(i => i.ApplicationType).Distinct(), token);
        var records = new HashSet<Guid>();
        var employees = new HashSet<Guid>();
        foreach (var instance in instances)
        {
            if (!applicants.TryGetValue(instance.ApplicantEmployeeId, out var applicant)) continue;
            if (!await IsCallersRequestAsync(instance, applicant, caller, resolver, approvers,
                    delegatorsByType.GetValueOrDefault(instance.ApplicationType) ?? [], token)) continue;
            records.Add(instance.Id);
            records.Add(instance.ApplicationId);
            employees.Add(instance.ApplicantEmployeeId);
        }

        ApprovalVisibleRecordIds = records.ToArray();
        ApprovalVisibleEmployeeIds = employees.ToArray();
    }

    private static async Task<bool> IsCallersRequestAsync(
        ApprovalInstance instance, Employee applicant, Employee caller, ApproverEligibilityResolver resolver,
        ApprovalApproverResolver approvers, IReadOnlyCollection<Employee> delegators, CancellationToken token)
    {
        if (instance.Actions.Any(a => a.ActorEmployeeId == caller.Id || a.OnBehalfOfEmployeeId == caller.Id)) return true;
        if (instance.ReassignedApproverEmployeeId == caller.Id) return true;
        if (instance.Status != ApprovalInstanceStatus.InProgress) return false;
        if (instance.EscalatedToEmployeeId == caller.Id) return true;

        var step = instance.Workflow?.Steps.FirstOrDefault(s => s.StepNumber == instance.CurrentStepNumber);
        if (step == null) return false;

        // Dynamic steps point at someone per applicant; delegation extends "the caller" to the
        // people who have delegated to them -- so a delegate can open the request they act on.
        // A step with several pools: the caller may open the request if they could act for any of them.
        foreach (var pool in ApprovalStepPools.Of(step))
        {
            var resolved = ApprovalApproverResolver.IsDynamic(pool.Effective.ApproverType)
                ? (await approvers.ResolveAsync(pool.Effective, applicant, token)).EmployeeIds
                : null;
            var named = pool.GroupId == null ? step.NamedApprovers.Select(a => a.EmployeeId).ToArray() : [];
            if (resolver.IsEligible(new ApprovalEligibilityContext(
                    caller, applicant, pool.Effective, named, instance.ReassignedApproverEmployeeId, delegators, resolved)))
                return true;
        }
        return false;
    }

    // The people who have delegated their approvals to the caller right now, per application type.
    // Mirrors ApprovalDelegationService, but over the raw context this scope runs on.
    private static async Task<Dictionary<ApprovalApplicationType, IReadOnlyCollection<Employee>>> LoadDelegatorsAsync(
        HrmsContext db, Guid tenantId, Guid callerEmployeeId, IEnumerable<ApprovalApplicationType> types, CancellationToken token)
    {
        var result = new Dictionary<ApprovalApplicationType, IReadOnlyCollection<Employee>>();
        var delegations = await db.ApprovalDelegations.IgnoreQueryFilters().AsNoTracking()
            .Where(d => d.TenantId == tenantId && d.DelegateEmployeeId == callerEmployeeId && d.IsActive)
            .ToListAsync(token);
        if (delegations.Count == 0) return result;

        var today = PhTime.Today(DateTime.UtcNow);
        var needLeave = delegations.Where(d => d.OnlyWhileOnLeave).Select(d => d.DelegatorEmployeeId).Distinct().ToList();
        var onLeave = needLeave.Count == 0
            ? new List<Guid>()
            : await db.leaveApplications.IgnoreQueryFilters().AsNoTracking()
                .Where(a => a.TenantId == tenantId && needLeave.Contains(a.EmployeeId) && a.ApprovalStatus == ApprovalStatus.Approved &&
                            a.LeaveDateFrom <= today && a.LeaveDateTo >= today)
                .Select(a => a.EmployeeId).Distinct().ToListAsync(token);

        var delegators = await db.Employees.IgnoreQueryFilters().AsNoTracking()
            .Where(e => delegations.Select(d => d.DelegatorEmployeeId).Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, token);

        foreach (var type in types)
        {
            var inForce = delegations
                .Where(d => ApprovalDelegationRules.IsInForce(d, type, today, onLeave.Contains(d.DelegatorEmployeeId)))
                .Select(d => delegators.GetValueOrDefault(d.DelegatorEmployeeId))
                .OfType<Employee>()
                .DistinctBy(e => e.Id)
                .ToList();
            if (inForce.Count > 0) result[type] = inForce;
        }
        return result;
    }
}
