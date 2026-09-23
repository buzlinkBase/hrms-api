namespace Hrms.Domain.ValueObjects;

// Batch-list projection for the Payroll Batches tab (Approve/Decline/Delete a whole Generate
// run) — mirrors BatchesModel (DTR's equivalent, see DTRDetailModel.cs), backed directly by
// PayrollBatch rather than derived by grouping child Payroll rows client-side.
// See PayrollBatchService.GetBatchesAsync.
public class PayrollBatchListModel
{
    public Guid Id { get; set; }
    public DateOnly PayPeriodStart { get; set; }
    public DateOnly PayPeriodEnd { get; set; }
    public DateOnly? PayDate { get; set; }
    public string? Remarks { get; set; }
    public int EmployeeCount { get; set; }
    public bool IsPosted { get; set; }
    public ApprovalStatus ApprovalStatus { get; set; }
    public Guid GeneratedByEmployeeId { get; set; }
    public Guid? PostedBy { get; set; }
    public PayrollType PayrollType { get; set; }
    // See PayrollBatch.PendingDeletion/RequestedDeletionByEmployeeId.
    public bool PendingDeletion { get; set; }
    public Guid? RequestedDeletionByEmployeeId { get; set; }
}
