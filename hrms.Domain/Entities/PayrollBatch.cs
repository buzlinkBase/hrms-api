using BuzlinkRepository;

namespace Hrms.Domain.Entities;

// Header/master row for one Generate run — every Payroll row it produces links back here
// via Payroll.PayrollBatchId. Centralizes the batch-level facts (period, pay date,
// remarks, posted status) that used to be duplicated across every employee's Payroll row,
// and is what Post/Delete Payroll Run (PayrollProcessorService.PostBatchAsync/
// DeleteBatchAsync) operate against directly instead of aggregating over child rows.
// DisableSoftDelete: PayrollBatchLifecycleService.DeleteBatchAsync hard-deletes every child
// row (Payroll, PayrollDtrDetail, PayrollDeductionDetail, SSS/PHIC/HDMF/WTax contribution
// ledgers) via ExecuteDeleteAsync/DB cascade — this header row must go the same way, or a
// deleted run would leave a soft-deleted PayrollBatch ghost behind with no children left to
// show for it.
[DisableSoftDelete]
public class PayrollBatch : BaseEntity
{
    public DateOnly PayPeriodStart { get; set; }
    public DateOnly PayPeriodEnd { get; set; }
    // Raw admin-entered Pay/Release Date from the payroll run request — see Payroll.PayDate.
    public DateOnly? PayDate { get; set; }
    // Comma-separated DTR batch code(s) (DailyRecord.BatchCode) this run was generated
    // from — see PayrollBatchService.GetUsedDtrBatchCodesAsync, which blocks re-generating
    // payroll from a DTR batch that's already been used here.
    public string? DtrBatchCodes { get; set; }
    // Free-text identity for this run, captured via the Generate Payroll confirmation
    // dialog — see PayrollRunPayload.Remarks.
    public string? Remarks { get; set; }
    public bool IsPosted { get; set; }
    public DateTime? PostedAt { get; set; }
    // Set to the final approver's employee id once the PayrollPosting approval instance
    // resolves to Approved — see PayrollBatchLifecycleService.ApproveBatchAsync.
    public Guid? PostedBy  { get; set; }
    // Regular (DTR-cutoff-driven) vs a 13th month pay run — see
    // PayrollProcessorService.GenerateThirteenthMonthAsync. Canonical source; Payroll.PayrollType
    // is a denormalized per-row copy, same pattern as IsPosted.
    public PayrollType PayrollType { get; set; } = PayrollType.Regular;
    // Who clicked Generate/Save — the ApprovalInstance's ApplicantEmployeeId for this batch's
    // PayrollPosting approval. See PayrollProcessorService/ThirteenthMonthPayrollService/
    // LastPayrollService/TaxAnnualizationService's Generate methods.
    public Guid GeneratedByEmployeeId { get; set; }
    // One shared ApprovalApplicationType.PayrollPosting approval type covers all 4 run types
    // (Regular/13th Month/Last Pay/Year-End Adjustment) since they already share this same
    // entity/Post/Delete code path. Post/DeleteBatch are only reachable once this reaches
    // Approved/stays ForApproval respectively — see PayrollBatchLifecycleService.
    // Stays Approved through a pending deletion request below -- PendingDeletion is a separate
    // concern, not a step backwards in posting status.
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.ForApproval;

    // An already-posted (Approved) batch can't be deleted outright -- deleting it needs its own
    // approval, same mechanism as posting but a separate ApprovalApplicationType.
    // PayrollPostingDeletion instance (a resolved ApprovalInstance can't be reopened, so posting
    // approval and deletion approval can't share one instance). The batch stays fully
    // visible/usable while a deletion request is pending -- see
    // PayrollBatchLifecycleService.RequestDeletionAsync/ApproveDeletionAsync/DeclineDeletionAsync.
    // Mirrors DTRBatch.PendingDeletion exactly.
    public bool PendingDeletion { get; set; }
    // Who clicked "Request Deletion" -- the PayrollPostingDeletion ApprovalInstance's
    // ApplicantEmployeeId.
    public Guid? RequestedDeletionByEmployeeId { get; set; }
}
