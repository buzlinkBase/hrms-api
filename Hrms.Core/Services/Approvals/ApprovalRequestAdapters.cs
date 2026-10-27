using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;

namespace Hrms.Core.Services.Approvals;

/// <summary>What the My Approvals list shows for one request.</summary>
public sealed record ApprovalRequestSummary(string Summary, DateTime FiledAt);

/// <summary>
/// One adapter per employee request type (Strategy): how to describe a request in a list, and how
/// to approve or decline it through the module's own service -- which runs the approval engine and
/// the type's side effects (leave credits, DTR changes...). The engine itself never touches a
/// module's status, so My Approvals must go through the module, never the engine alone.
/// </summary>
public interface IApprovalRequestAdapter
{
    ApprovalApplicationType Type { get; }

    /// <summary>The permission row whose :Approve code gates direct approvers of this type.</summary>
    string PermissionPrefix { get; }

    /// <summary>Summaries for the requests the caller is allowed to see; a missing id is simply left out.</summary>
    Task<Dictionary<Guid, ApprovalRequestSummary>> DescribeAsync(IReadOnlyCollection<Guid> ids, CancellationToken token);

    Task ApproveAsync(Guid id, Guid? approverEmployeeId, bool hasOverride, string? note, CancellationToken token);
    Task DeclineAsync(Guid id, Guid? approverEmployeeId, bool hasOverride, string? note, CancellationToken token);
}

internal static class ApprovalSummaryFormat
{
    public static string Range(DateOnly from, DateOnly to) =>
        from == to ? from.ToString("MMM d") : $"{from:MMM d} – {to:MMM d}";

    public static string Hours(double minutes) => $"{Math.Round(minutes / 60d, 2):0.##} h";
}

public class LeaveApprovalAdapter : IApprovalRequestAdapter
{
    private readonly IUnitOfWorkService _uow;
    private readonly LeaveApplicationService _service;
    public LeaveApprovalAdapter(IUnitOfWorkService uow, LeaveApplicationService service) { _uow = uow; _service = service; }

    public ApprovalApplicationType Type => ApprovalApplicationType.Leave;
    public string PermissionPrefix => "Leave";

    public async Task<Dictionary<Guid, ApprovalRequestSummary>> DescribeAsync(IReadOnlyCollection<Guid> ids, CancellationToken token)
    {
        var list = ids.ToList();
        var rows = await _uow.Repository.Find<LeaveApplication>(a => list.Contains(a.Id)).Include(a => a.Leave).AsNoTracking().ToListAsync(token);
        return rows.ToDictionary(a => a.Id, a => new ApprovalRequestSummary(
            $"{a.Leave?.Description ?? "Leave"} · {ApprovalSummaryFormat.Range(a.LeaveDateFrom, a.LeaveDateTo)}", a.CreatedAt));
    }

    public Task ApproveAsync(Guid id, Guid? approverEmployeeId, bool hasOverride, string? note, CancellationToken token) =>
        _service.ApproveAsync(id, approverEmployeeId, hasOverride, note, token);
    public Task DeclineAsync(Guid id, Guid? approverEmployeeId, bool hasOverride, string? note, CancellationToken token) =>
        _service.DeclineAsync(id, approverEmployeeId, hasOverride, note, token);
}

public class OvertimeApprovalAdapter : IApprovalRequestAdapter
{
    private readonly IUnitOfWorkService _uow;
    private readonly OvertimeApplicationService _service;
    public OvertimeApprovalAdapter(IUnitOfWorkService uow, OvertimeApplicationService service) { _uow = uow; _service = service; }

    public ApprovalApplicationType Type => ApprovalApplicationType.Overtime;
    public string PermissionPrefix => "Overtime";

    public async Task<Dictionary<Guid, ApprovalRequestSummary>> DescribeAsync(IReadOnlyCollection<Guid> ids, CancellationToken token)
    {
        var list = ids.ToList();
        var rows = await _uow.Repository.Find<OverTimeApplication>(a => list.Contains(a.Id)).AsNoTracking().ToListAsync(token);
        return rows.ToDictionary(a => a.Id, a =>
        {
            var minutes = a.ManualOTMinutes > 0
                ? a.ManualOTMinutes
                : a.StartTime.HasValue && a.EndTime.HasValue ? (a.EndTime.Value - a.StartTime.Value).TotalMinutes : 0;
            return new ApprovalRequestSummary($"Overtime · {a.OTDate:MMM d} · {ApprovalSummaryFormat.Hours(minutes)}", a.CreatedAt);
        });
    }

    public Task ApproveAsync(Guid id, Guid? approverEmployeeId, bool hasOverride, string? note, CancellationToken token) =>
        _service.ApproveAsync(id, approverEmployeeId, hasOverride, note, token);
    public Task DeclineAsync(Guid id, Guid? approverEmployeeId, bool hasOverride, string? note, CancellationToken token) =>
        _service.DeclineAsync(id, approverEmployeeId, hasOverride, note, token);
}

public class OfficialBusinessApprovalAdapter : IApprovalRequestAdapter
{
    private readonly IUnitOfWorkService _uow;
    private readonly TravelOrderApplicationService _service;
    public OfficialBusinessApprovalAdapter(IUnitOfWorkService uow, TravelOrderApplicationService service) { _uow = uow; _service = service; }

    public ApprovalApplicationType Type => ApprovalApplicationType.OfficialBusiness;
    public string PermissionPrefix => "Official Business";

    public async Task<Dictionary<Guid, ApprovalRequestSummary>> DescribeAsync(IReadOnlyCollection<Guid> ids, CancellationToken token)
    {
        var list = ids.ToList();
        var rows = await _uow.Repository.Find<TravelOrderApplication>(a => list.Contains(a.Id)).AsNoTracking().ToListAsync(token);
        return rows.ToDictionary(a => a.Id, a => new ApprovalRequestSummary(
            $"Official business · {a.Destination} · {ApprovalSummaryFormat.Range(a.StartDate, a.EndDate)}", a.CreatedAt));
    }

    public Task ApproveAsync(Guid id, Guid? approverEmployeeId, bool hasOverride, string? note, CancellationToken token) =>
        _service.ApproveAsync(id, approverEmployeeId, hasOverride, note, token);
    public Task DeclineAsync(Guid id, Guid? approverEmployeeId, bool hasOverride, string? note, CancellationToken token) =>
        _service.DeclineAsync(id, approverEmployeeId, hasOverride, note, token);
}

public class PassSlipApprovalAdapter : IApprovalRequestAdapter
{
    private readonly IUnitOfWorkService _uow;
    private readonly PassSlipApplicationService _service;
    public PassSlipApprovalAdapter(IUnitOfWorkService uow, PassSlipApplicationService service) { _uow = uow; _service = service; }

    public ApprovalApplicationType Type => ApprovalApplicationType.PassSlip;
    public string PermissionPrefix => "Pass Slip";

    public async Task<Dictionary<Guid, ApprovalRequestSummary>> DescribeAsync(IReadOnlyCollection<Guid> ids, CancellationToken token)
    {
        var list = ids.ToList();
        var rows = await _uow.Repository.Find<PassSlipApplication>(a => list.Contains(a.Id)).AsNoTracking().ToListAsync(token);
        return rows.ToDictionary(a => a.Id, a => new ApprovalRequestSummary(
            $"Pass slip · {a.Purpose} · {a.DepartureTime:MMM d, h:mm tt}", a.CreatedAt));
    }

    public Task ApproveAsync(Guid id, Guid? approverEmployeeId, bool hasOverride, string? note, CancellationToken token) =>
        _service.ApproveAsync(id, approverEmployeeId, hasOverride, note, token);
    public Task DeclineAsync(Guid id, Guid? approverEmployeeId, bool hasOverride, string? note, CancellationToken token) =>
        _service.DeclineAsync(id, approverEmployeeId, hasOverride, note, token);
}

public class LoanApprovalAdapter : IApprovalRequestAdapter
{
    private readonly IUnitOfWorkService _uow;
    private readonly DeductionApplicationService _service;
    public LoanApprovalAdapter(IUnitOfWorkService uow, DeductionApplicationService service) { _uow = uow; _service = service; }

    public ApprovalApplicationType Type => ApprovalApplicationType.Loan;
    public string PermissionPrefix => "Loan/Deduction";

    public async Task<Dictionary<Guid, ApprovalRequestSummary>> DescribeAsync(IReadOnlyCollection<Guid> ids, CancellationToken token)
    {
        var list = ids.ToList();
        var rows = await _uow.Repository.Find<DeductionApplication>(a => list.Contains(a.Id)).AsNoTracking().ToListAsync(token);
        return rows.ToDictionary(a => a.Id, a => new ApprovalRequestSummary(
            $"Loan/deduction · ₱{a.TotalAmount:N2} · from {a.StartDate:MMM d, yyyy}", a.CreatedAt));
    }

    public Task ApproveAsync(Guid id, Guid? approverEmployeeId, bool hasOverride, string? note, CancellationToken token) =>
        _service.ApproveAsync(id, approverEmployeeId, hasOverride, note, token);
    public Task DeclineAsync(Guid id, Guid? approverEmployeeId, bool hasOverride, string? note, CancellationToken token) =>
        _service.DeclineAsync(id, approverEmployeeId, hasOverride, note, token);
}

public class ProfileUpdateApprovalAdapter : IApprovalRequestAdapter
{
    private readonly IUnitOfWorkService _uow;
    private readonly EmployeeProfileUpdateRequestService _service;
    public ProfileUpdateApprovalAdapter(IUnitOfWorkService uow, EmployeeProfileUpdateRequestService service) { _uow = uow; _service = service; }

    public ApprovalApplicationType Type => ApprovalApplicationType.ProfileUpdate;
    public string PermissionPrefix => "Profile Update";

    public async Task<Dictionary<Guid, ApprovalRequestSummary>> DescribeAsync(IReadOnlyCollection<Guid> ids, CancellationToken token)
    {
        var list = ids.ToList();
        var rows = await _uow.Repository.Find<EmployeeProfileUpdateRequest>(a => list.Contains(a.Id)).AsNoTracking().ToListAsync(token);
        return rows.ToDictionary(a => a.Id, a => new ApprovalRequestSummary("Profile update request", a.CreatedAt));
    }

    // forceApply stays false: overriding a conflicting change is a deliberate admin action on the
    // Profile Update screen, not something to do from an inbox.
    public Task ApproveAsync(Guid id, Guid? approverEmployeeId, bool hasOverride, string? note, CancellationToken token) =>
        _service.ApproveAsync(id, approverEmployeeId, hasOverride, note, forceApply: false, token);
    public Task DeclineAsync(Guid id, Guid? approverEmployeeId, bool hasOverride, string? note, CancellationToken token) =>
        _service.DeclineAsync(id, approverEmployeeId, hasOverride, note, token);
}
