using BuzlinkRepository;

namespace Hrms.Domain.Entities;

// Header/master row for one DTR Save Draft action — every DailyRecord row it produces shares
// its BatchCode (a plain string, not a real FK, matching how DailyRecord already keys batches
// today). Mirrors PayrollBatch's role for Payroll runs: centralizes the batch-level approval
// facts (who generated it, posted status, approval status) that used to have no home at all —
// DTR batches previously had no header entity, just an implicit GroupBy(BatchCode) over
// DailyRecord rows (see DailyRecordService.GetBatches). PostingDescription deliberately isn't
// duplicated here — it's still read straight off the first DailyRecord row per batch, same as
// before this entity existed.
[DisableSoftDelete]
public class DTRBatch : BaseEntity
{
    public string BatchCode { get; set; } = string.Empty;
    public DateOnly PayPeriodStart { get; set; }
    public DateOnly PayPeriodEnd { get; set; }
    public Guid? PayrollGroupId { get; set; }
    // Who clicked Save Draft — the ApprovalInstance's ApplicantEmployeeId for this batch's
    // Dtr-type approval. See DailyRecordService.SaveDraftAsync.
    public Guid GeneratedByEmployeeId { get; set; }
    public bool IsPosted { get; set; }
    public DateTime? PostedAt { get; set; }
    // Set to the final approver's employee id once the Dtr approval instance resolves to
    // Approved — see DailyRecordService.ApproveBatchAsync.
    public Guid? PostedBy { get; set; }
    // ForApproval until the configured workflow (or the implicit single-approval fallback, same
    // as every other application type) resolves it. Only an Approved batch actually flips
    // IsPosted/DailyRecord.Posted — see DailyRecordService.ApproveBatchAsync. Payroll generation
    // refuses to run from a batch that isn't Approved — see PayrollProcessorService.GenerateAsync.
    // Stays Approved through a pending deletion request below -- PendingDeletion is a separate
    // concern, not a step backwards in posting status.
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.ForApproval;

    // An already-posted (Approved) batch can't be deleted outright -- deleting it needs its own
    // approval, same mechanism as posting but a separate ApprovalApplicationType.DtrDeletion
    // instance (a resolved ApprovalInstance can't be reopened, so posting approval and deletion
    // approval can't share one instance). The batch stays fully visible/usable while a deletion
    // request is pending -- see DailyRecordService.RequestDeletionAsync/ApproveDeletionAsync/
    // DeclineDeletionAsync. An unposted (ForApproval/Declined) batch is never PendingDeletion --
    // it's just deleted directly, nothing about it is final yet.
    public bool PendingDeletion { get; set; }
    // Who clicked "Request Deletion" -- the DtrDeletion ApprovalInstance's ApplicantEmployeeId.
    public Guid? RequestedDeletionByEmployeeId { get; set; }
}
