namespace Hrms.Domain.ValueObjects;

public class CreateLeaveApplication
{
    public Guid LeaveId { get; set; }
    public Guid EmployeeId { get; set; }
    public DurationType DurationType { get; set; } = DurationType.SingleDay;
    public DateOnly LeaveDateFrom { get; set; }
    public DateOnly LeaveDateTo { get; set; }
    public DayFraction DayFraction { get; set; } = DayFraction.FullDay;
    public PayType PayType { get; set; } = PayType.WithPay;
    public PayoutMode PayoutMode { get; set; } = PayoutMode.PerDay;
    public decimal? GovernmentAmount { get; set; }
    public decimal? CompanyAmount { get; set; }
    public DateOnly? ReleasePayrollDate { get; set; }
    // Null = inherit the leave type's Leave.EmployerAdvancesPayment default — see
    // LeaveApplication.EmployerAdvancesPayment.
    public bool? EmployerAdvancesPayment { get; set; }
    public bool IsManualEntry { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int? TotalMinutes { get; set; }
    public string? ApplicationRemarks { get; set; }
    public string? SupportingDocumentUrl { get; set; }
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.ForApproval;
}

public class UpdateLeaveApplication : CreateLeaveApplication
{
    public Guid Id { get; set; }

    // Approver's note for an approve/decline transition -- required/optional/ignored per the
    // current approval step's NoteRequirement (see ApprovalEngineService.RecordActionAsync).
    // Meaningless on a plain field edit that doesn't change ApprovalStatus.
    public string? Note { get; set; }
}

public class LeaveApplicationModel : UpdateLeaveApplication
{
    public int? ReviewedBy { get; set; }
    public DateTime? ReviewedOn { get; set; }
    public ReimbursementStatus ReimbursementStatus { get; set; }
    public DateOnly? ReimbursementFiledDate { get; set; }
    public DateOnly? ReimbursementReceivedDate { get; set; }
    public string? ReimbursementReferenceNo { get; set; }
}

// Finance-only update to a LeaveApplication's reimbursement claim tracking — deliberately
// separate from CreateLeaveApplication/UpdateLeaveApplication so marking a claim Filed/
// Reimbursed never requires resending (or risks overwriting) the full leave application.
// See LeaveApplicationService.UpdateReimbursementStatusAsync.
public class UpdateReimbursementStatus
{
    public ReimbursementStatus Status { get; set; }
    public DateOnly? FiledDate { get; set; }
    public DateOnly? ReceivedDate { get; set; }
    public string? ReferenceNo { get; set; }
}
