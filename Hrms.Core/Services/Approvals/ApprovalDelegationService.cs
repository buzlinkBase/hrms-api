using Hrms.Domain.Entities;
using Hrms.Domain.Entities.Approvals;
using Onepunch.Common.Lib.Exceptions;

namespace Hrms.Core.Services.Approvals;

/// <summary>
/// What the approval engine needs to know about delegation, and nothing more -- the engine takes
/// this as an optional dependency, so tests and callers without delegation simply pass none.
/// </summary>
public interface IApprovalDelegationLookup
{
    /// <summary>The employees who have delegated their approvals to <paramref name="delegateEmployeeId"/> right now, for this type.</summary>
    Task<IReadOnlyList<Guid>> GetActiveDelegatorIdsAsync(
        Guid delegateEmployeeId, ApprovalApplicationType type, CancellationToken token);

    /// <summary>The (delegator, delegate) pairs in force right now for these delegators and this type.</summary>
    Task<IReadOnlyList<(Guid DelegatorId, Guid DelegateId)>> GetActiveDelegatesAsync(
        IReadOnlyCollection<Guid> delegatorEmployeeIds, ApprovalApplicationType type, CancellationToken token);
}

/// <summary>Pure rules for when a delegation is in force, so they are testable without a database.</summary>
public static class ApprovalDelegationRules
{
    public static bool CoversType(ApprovalDelegation delegation, ApprovalApplicationType type) =>
        delegation.ApplicationType is null || delegation.ApplicationType == type;

    public static bool InWindow(ApprovalDelegation delegation, DateOnly today) =>
        (delegation.FromDate is null || delegation.FromDate <= today) &&
        (delegation.ToDate is null || today <= delegation.ToDate);

    /// <summary>In force today, given whether its delegator is on approved leave today.</summary>
    public static bool IsInForce(ApprovalDelegation delegation, ApprovalApplicationType type, DateOnly today, bool delegatorOnLeave) =>
        delegation.IsActive &&
        CoversType(delegation, type) &&
        InWindow(delegation, today) &&
        (!delegation.OnlyWhileOnLeave || delegatorOnLeave);
}

// Setup for "while I'm away" delegation: each person manages their own (profile card) and an
// admin can set one up for someone. The engine reads it through IApprovalDelegationLookup.
public class ApprovalDelegationService : BaseService<ApprovalDelegation>, IApprovalDelegationLookup
{
    public ApprovalDelegationService(IUnitOfWorkService uow) : base(uow) { }

    private static DateOnly Today() => PhTime.Today(DateTime.UtcNow);

    public async Task<IReadOnlyList<Guid>> GetActiveDelegatorIdsAsync(
        Guid delegateEmployeeId, ApprovalApplicationType type, CancellationToken token)
    {
        var candidates = await Context.ApprovalDelegations.AsNoTracking()
            .Where(d => d.DelegateEmployeeId == delegateEmployeeId && d.IsActive)
            .ToListAsync(token);
        return (await InForceAsync(candidates, type, token)).Select(d => d.DelegatorEmployeeId).Distinct().ToList();
    }

    public async Task<IReadOnlyList<(Guid DelegatorId, Guid DelegateId)>> GetActiveDelegatesAsync(
        IReadOnlyCollection<Guid> delegatorEmployeeIds, ApprovalApplicationType type, CancellationToken token)
    {
        if (delegatorEmployeeIds.Count == 0) return [];
        var ids = delegatorEmployeeIds.ToList();
        var candidates = await Context.ApprovalDelegations.AsNoTracking()
            .Where(d => ids.Contains(d.DelegatorEmployeeId) && d.IsActive)
            .ToListAsync(token);
        return (await InForceAsync(candidates, type, token))
            .Select(d => (d.DelegatorEmployeeId, d.DelegateEmployeeId))
            .Distinct()
            .ToList();
    }

    // The leave check only runs for the "while on leave" delegations, in one query for all of them.
    private async Task<List<ApprovalDelegation>> InForceAsync(
        List<ApprovalDelegation> candidates, ApprovalApplicationType type, CancellationToken token)
    {
        var today = Today();
        var relevant = candidates.Where(d => ApprovalDelegationRules.CoversType(d, type) && ApprovalDelegationRules.InWindow(d, today)).ToList();

        var needLeave = relevant.Where(d => d.OnlyWhileOnLeave).Select(d => d.DelegatorEmployeeId).Distinct().ToList();
        var onLeave = needLeave.Count == 0
            ? []
            : await Context.IgnoringConfidentiality<LeaveApplication>()
                .Where(a => needLeave.Contains(a.EmployeeId) && a.ApprovalStatus == ApprovalStatus.Approved &&
                            a.LeaveDateFrom <= today && a.LeaveDateTo >= today)
                .Select(a => a.EmployeeId)
                .Distinct()
                .ToListAsync(token);

        return relevant.Where(d => ApprovalDelegationRules.IsInForce(d, type, today, onLeave.Contains(d.DelegatorEmployeeId))).ToList();
    }

    public Task<List<ApprovalDelegation>> ListForDelegatorAsync(Guid delegatorEmployeeId, CancellationToken token = default) =>
        Context.ApprovalDelegations.AsNoTracking()
            .Where(d => d.DelegatorEmployeeId == delegatorEmployeeId)
            .OrderByDescending(d => d.IsActive).ThenByDescending(d => d.CreatedAt)
            .ToListAsync(token);

    /// <summary>What others have delegated to this person (active ones), so they know they can act for them.</summary>
    public Task<List<ApprovalDelegation>> ListToDelegateAsync(Guid delegateEmployeeId, CancellationToken token = default) =>
        Context.ApprovalDelegations.AsNoTracking()
            .Where(d => d.DelegateEmployeeId == delegateEmployeeId && d.IsActive)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(token);

    /// <summary>Someone who can be chosen to act on another's behalf: has a login, isn't the chooser.</summary>
    public sealed record DelegateCandidate(Guid Id, string Name);

    /// <summary>
    /// Who the person may delegate to -- people with a login, found by name or employee number. The
    /// Employee Portal can't load the full employee list, so this is its own small lookup. Anyone
    /// the caller isn't allowed to see (confidential) is already left out by the query filter.
    /// </summary>
    public async Task<List<DelegateCandidate>> FindCandidatesAsync(Guid exceptEmployeeId, string? search, CancellationToken token = default)
    {
        var query = Context.Employees.AsNoTracking()
            .Where(e => e.UserId != null && e.Id != exceptEmployeeId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(e => e.FirstName.Contains(term) || e.LastName.Contains(term) || e.EmployeeNo.Contains(term));
        }

        var rows = await query.OrderBy(e => e.LastName).ThenBy(e => e.FirstName).Take(50)
            .Select(e => new { e.Id, e.FirstName, e.LastName })
            .ToListAsync(token);
        return rows.Select(e => new DelegateCandidate(e.Id, $"{e.FirstName} {e.LastName}".Trim())).ToList();
    }

    public Task<List<ApprovalDelegation>> ListAllAsync(CancellationToken token = default) =>
        Context.ApprovalDelegations.AsNoTracking()
            .OrderByDescending(d => d.IsActive).ThenByDescending(d => d.CreatedAt)
            .ToListAsync(token);

    public async Task<ApprovalDelegation> CreateDelegationAsync(ApprovalDelegation delegation, CancellationToken token = default)
    {
        if (delegation.DelegatorEmployeeId == delegation.DelegateEmployeeId)
            throw new InvalidOperationException("You can't delegate to yourself.");
        if (delegation.FromDate is { } from && delegation.ToDate is { } to && to < from)
            throw new InvalidOperationException("The end date can't be before the start date.");
        if (delegation.ToDate is { } end && end < Today())
            throw new InvalidOperationException("The end date has already passed.");

        var delegateEmployee = await Context.Employees.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == delegation.DelegateEmployeeId, token)
            ?? throw new NotFoundException("The person to delegate to wasn't found.");
        // Acting on approvals needs a login; a delegate without one could never use it.
        if (delegateEmployee.UserId is null)
            throw new InvalidOperationException("That person doesn't have a login yet, so they can't approve on your behalf.");

        if (!await Context.Employees.AsNoTracking().AnyAsync(e => e.Id == delegation.DelegatorEmployeeId, token))
            throw new NotFoundException("The delegating employee wasn't found.");

        delegation.IsActive = true;
        await CreateAsync(delegation, token);
        await CommitChangesAsync(token);
        return delegation;
    }

    /// <summary>
    /// Switches a delegation off. <paramref name="onlyForDelegator"/> limits it to that person's own
    /// (self-service); null lets an admin switch off any.
    /// </summary>
    public async Task DeactivateAsync(Guid id, Guid? onlyForDelegator, CancellationToken token = default)
    {
        var delegation = await Context.ApprovalDelegations.FirstOrDefaultAsync(d => d.Id == id, token)
            ?? throw new NotFoundException("Delegation not found.");
        if (onlyForDelegator is { } owner && delegation.DelegatorEmployeeId != owner)
            throw new NotFoundException("Delegation not found.");
        delegation.IsActive = false;
        await CommitChangesAsync(token);
    }
}
