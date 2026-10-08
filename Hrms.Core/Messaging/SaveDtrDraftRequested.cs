namespace Hrms.Core.Messaging;

/// <summary>
/// "Save this cutoff's DTR as a draft" -- queued by POST dailyrecords/save-draft and carried out by
/// <see cref="SaveDtrDraftWorker"/>, so a large company's save never runs inside (or waits on) one
/// HTTP request. Carries what the rows are calculated from, not the rows themselves.
/// </summary>
public record SaveDtrDraftRequested
{
    public DateOnly FromDate { get; init; }
    public DateOnly ToDate { get; init; }
    public Guid? PayrollGroupId { get; init; }
    public Guid? BranchId { get; init; }
    public Guid? DepartmentId { get; init; }
    public Guid? ClientId { get; init; }
    public Guid? OperationAreaId { get; init; }
    public List<Guid> EmployeeIds { get; init; } = [];
    public string? PostingDescription { get; init; }

    /// <summary>The login that asked, stamped on every saved row.</summary>
    public Guid UserId { get; init; }

    /// <summary>The employee the batch (and its approval) is attributed to, and who is told the outcome.</summary>
    public Guid GeneratedByEmployeeId { get; init; }
}
