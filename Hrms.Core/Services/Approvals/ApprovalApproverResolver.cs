using Hrms.Domain.Entities;
using Hrms.Domain.Entities.Approvals;
using Hrms.Domain.Entities.EmployeeEntities;

namespace Hrms.Core.Services.Approvals;

/// <summary>A department's head and its parent -- all the resolver needs to climb the department tree.</summary>
public sealed record DepartmentNode(Guid? HeadId, Guid? ParentDepartmentId);

/// <summary>
/// Reads the org structure the dynamic approver types resolve against. Two implementations: one over
/// the unit of work (the engine) and one over a raw context (the confidential-access scope, which runs
/// outside a request's query filters). Kept this small so the resolution rules stay testable with a fake.
/// </summary>
public interface IApproverOrgLookup
{
    Task<Employee?> GetEmployeeAsync(Guid id, CancellationToken token);
    Task<DepartmentNode?> GetDepartmentAsync(Guid id, CancellationToken token);
    Task<Guid?> GetSectionHeadAsync(Guid id, CancellationToken token);
    Task<Guid?> GetBranchHeadAsync(Guid id, CancellationToken token);
    Task<Guid?> GetAreaHeadAsync(Guid id, CancellationToken token);
    Task<Guid?> GetClientHeadAsync(Guid id, CancellationToken token);
}

/// <summary>Who a dynamic step resolves to for one applicant. Unresolved = nobody could be found and there is no fallback.</summary>
public sealed record ResolvedApprovers(IReadOnlyList<Guid> EmployeeIds, bool Unresolved)
{
    public static ResolvedApprovers Of(params Guid[] ids) => new(ids, false);
    public static readonly ResolvedApprovers NotFound = new([], true);
}

/// <summary>
/// Works out, per applicant, who a dynamic approver step points at: the manager N levels up, the
/// head of the applicant's department/section/branch/site/client. When nobody can be found the step's
/// policy applies: a fallback person takes it; otherwise the result is Unresolved and the engine
/// either blocks (waits for an admin) or skips the step. The applicant is never their own approver:
/// a head who files a request has it resolve to nobody (so the policy decides) rather than to themself.
/// </summary>
public class ApprovalApproverResolver
{
    /// <summary>Longest reporting line or department chain followed; also stops a cycle in bad data.</summary>
    public const int MaxLevel = 10;

    private readonly IApproverOrgLookup _org;

    public ApprovalApproverResolver(IApproverOrgLookup org) => _org = org;

    /// <summary>Types resolved here rather than by a fixed person, department or position.</summary>
    public static bool IsDynamic(ApproverType type) => type is
        ApproverType.ApplicantManager or
        ApproverType.ApplicantManagerLevel or
        ApproverType.ApplicantDepartmentHead or
        ApproverType.ApplicantSectionHead or
        ApproverType.ApplicantBranchHead or
        ApproverType.ApplicantAreaHead or
        ApproverType.ApplicantClientHead;

    public async Task<ResolvedApprovers> ResolveAsync(ApprovalWorkflowStep step, Employee applicant, CancellationToken token)
    {
        var level = Math.Clamp(step.ApproverLevel ?? 1, 1, MaxLevel);
        var found = step.ApproverType switch
        {
            ApproverType.ApplicantManager => applicant.ManagerId,
            ApproverType.ApplicantManagerLevel => await ManagerAsync(applicant, level, token),
            ApproverType.ApplicantDepartmentHead => await DepartmentHeadAsync(applicant, level, token),
            ApproverType.ApplicantSectionHead => applicant.SectionId is { } s ? await _org.GetSectionHeadAsync(s, token) : null,
            ApproverType.ApplicantBranchHead => applicant.BranchId is { } b ? await _org.GetBranchHeadAsync(b, token) : null,
            ApproverType.ApplicantAreaHead => applicant.AreaId is { } a ? await _org.GetAreaHeadAsync(a, token) : null,
            ApproverType.ApplicantClientHead => applicant.ClientId is { } c ? await _org.GetClientHeadAsync(c, token) : null,
            _ => null,
        };

        if (found is { } approver && approver != applicant.Id) return ResolvedApprovers.Of(approver);

        return step.OnUnresolved == UnresolvedApproverPolicy.FallbackPerson && step.FallbackEmployeeId is { } fallback
            ? ResolvedApprovers.Of(fallback)
            : ResolvedApprovers.NotFound;
    }

    // Follows ManagerId up the reporting line; a loop (A -> B -> A) or a gap ends it with nobody.
    private async Task<Guid?> ManagerAsync(Employee applicant, int level, CancellationToken token)
    {
        var visited = new HashSet<Guid> { applicant.Id };
        var current = applicant;
        for (var i = 1; i <= level; i++)
        {
            if (current.ManagerId is not { } managerId || !visited.Add(managerId)) return null;
            if (i == level) return managerId;
            current = await _org.GetEmployeeAsync(managerId, token);
            if (current == null) return null;
        }
        return null;
    }

    // Level 1 = the applicant's own department's head, 2 = its parent's, and so on.
    private async Task<Guid?> DepartmentHeadAsync(Employee applicant, int level, CancellationToken token)
    {
        if (applicant.DepartmentId is not { } departmentId) return null;
        var visited = new HashSet<Guid>();
        Guid? current = departmentId;
        for (var i = 1; i <= level; i++)
        {
            if (current is not { } id || !visited.Add(id)) return null;
            var node = await _org.GetDepartmentAsync(id, token);
            if (node == null) return null;
            if (i == level) return node.HeadId;
            current = node.ParentDepartmentId;
        }
        return null;
    }
}

// The engine's view of the org structure: through the unit of work, with each lookup remembered for
// the life of the (scoped) instance so a list screen resolving many requests asks once per unit.
public class RepositoryOrgLookup : IApproverOrgLookup
{
    private readonly IUnitOfWorkService _uow;
    private readonly Dictionary<Guid, Employee?> _employees = [];
    private readonly Dictionary<Guid, DepartmentNode?> _departments = [];
    private readonly Dictionary<(string Kind, Guid Id), Guid?> _heads = [];

    public RepositoryOrgLookup(IUnitOfWorkService uow) => _uow = uow;

    public async Task<Employee?> GetEmployeeAsync(Guid id, CancellationToken token)
    {
        if (_employees.TryGetValue(id, out var cached)) return cached;
        return _employees[id] = await _uow.Repository.Find<Employee>(e => e.Id == id).AsNoTracking().FirstOrDefaultAsync(token);
    }

    public async Task<DepartmentNode?> GetDepartmentAsync(Guid id, CancellationToken token)
    {
        if (_departments.TryGetValue(id, out var cached)) return cached;
        var row = await _uow.Repository.Find<Department>(d => d.Id == id).AsNoTracking()
            .Select(d => new { d.HeadId, d.ParentDepartmentId }).FirstOrDefaultAsync(token);
        return _departments[id] = row == null ? null : new DepartmentNode(row.HeadId, row.ParentDepartmentId);
    }

    public Task<Guid?> GetSectionHeadAsync(Guid id, CancellationToken token) =>
        HeadAsync("section", id, async () => await _uow.Repository.Find<Section>(s => s.Id == id).AsNoTracking()
            .Select(s => s.HeadId).FirstOrDefaultAsync(token));

    public Task<Guid?> GetBranchHeadAsync(Guid id, CancellationToken token) =>
        HeadAsync("branch", id, async () => await _uow.Repository.Find<Branch>(b => b.Id == id).AsNoTracking()
            .Select(b => b.HeadEmployeeId).FirstOrDefaultAsync(token));

    public Task<Guid?> GetAreaHeadAsync(Guid id, CancellationToken token) =>
        HeadAsync("area", id, async () => await _uow.Repository.Find<CostCenters>(a => a.Id == id).AsNoTracking()
            .Select(a => a.HeadEmployeeId).FirstOrDefaultAsync(token));

    public Task<Guid?> GetClientHeadAsync(Guid id, CancellationToken token) =>
        HeadAsync("client", id, async () => await _uow.Repository.Find<Client>(c => c.Id == id).AsNoTracking()
            .Select(c => c.HeadEmployeeId).FirstOrDefaultAsync(token));

    private async Task<Guid?> HeadAsync(string kind, Guid id, Func<Task<Guid?>> load)
    {
        if (_heads.TryGetValue((kind, id), out var cached)) return cached;
        return _heads[(kind, id)] = await load();
    }
}
