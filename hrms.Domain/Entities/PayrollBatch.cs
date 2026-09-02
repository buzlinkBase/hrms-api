namespace Hrms.Domain.Entities;

// Header/master row for one Generate run — every Payroll row it produces links back here
// via Payroll.PayrollBatchId. Centralizes the batch-level facts (period, pay date,
// remarks, posted status) that used to be duplicated across every employee's Payroll row,
// and is what Post/Delete Payroll Run (PayrollProcessorService.PostBatchAsync/
// DeleteBatchAsync) operate against directly instead of aggregating over child rows.
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
    public Guid? PostedBy  { get; set; }
    // Regular (DTR-cutoff-driven) vs a 13th month pay run — see
    // PayrollProcessorService.GenerateThirteenthMonthAsync. Canonical source; Payroll.PayrollType
    // is a denormalized per-row copy, same pattern as IsPosted.
    public PayrollType PayrollType { get; set; } = PayrollType.Regular;
}
