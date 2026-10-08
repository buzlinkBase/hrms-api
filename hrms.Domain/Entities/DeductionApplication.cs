using BuzlinkRepository;
using Onepunch.AuditEngine;
namespace Hrms.Domain.Entities;

[Audited(EntityType = "Loan")]
public class DeductionApplication : BaseEntity, IEmployeeOwned, IEntryRecord, IApprovalSubject
{
    public Guid EmployeeId { get; set; }
    public Guid DeductionId { get; set; }
    public DateOnly EncodeDate { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public DeductionFrequency FrequencyOfPayment { get; set; }
    public int Terms { get; set; }
    public decimal TotalPrincipal { get; set; }
    public decimal InterestRate { get; set; }
    public decimal TotalAmount { get; set; }
    public string ProcessBy { get; set; }
    public string Note { get; set; }
    public string Remarks { get; set; }
    public virtual ICollection<DeductionApplicationDetail> Breakdown { get; set; }
    // Legacy default only -- every creation path (admin DeductionApplicationsController.Post and
    // self-service MeController.CreateMyLoanApplication) now sets ForApproval explicitly, and
    // DeductionApplicationService.UpdateAsync carries the stored status across edits, so this
    // default is never what a new/edited row ends up with. See DeductionAplDtlService.LoadAsync,
    // the only query the payroll deduction pipeline reads through.
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Approved;

    // ProcessBy is a NOT NULL column but neither CreateDeductionApplication nor
    // UpdateDeductionApplication carry it (there's no "who processed this" input on the
    // admin/portal Loan/Deduction Application forms), so Mapster's Map<DeductionApplication>()
    // leaves it at the CLR default (null) unless defaulted here -- mirrors
    // OtherIncomeApplication's identical ProcessBy default for the same reason.
    public DeductionApplication()
    {
        ProcessBy = "";
        Breakdown = new List<DeductionApplicationDetail>();
    }
}

// Hard-deleted: an installment payroll never touched has no history worth keeping, and one it
// did touch is locked (LoanScheduleReconciler) so it's never deleted at all.
[DisableSoftDelete]
public class DeductionApplicationDetail : BaseEntity, IEmployeeOwned, IEntryRecord
{
    public Guid EmployeeId { get; set; }
    public Guid DeductionId { get; set; }
    // The single FK to the owning loan (see DeductionApplicationConfig). Set through the
    // Breakdown navigation, never by hand -- the loan's Id is still Guid.Empty until EF adds it.
    public Guid DeductionApplicationId { get; set; }
    public DateOnly Date { get; set; }
    public decimal Principal { get; set; }
    public decimal Interest { get; set; }
    public int RecordOrder { get; set; }
    public decimal Amount { get; set; }
    public decimal Balance { get; set; }
    public string Notes { get; set; } = string.Empty;
    public DeductionApplicationDetail()
    {
        Status = "Unpaid";
    }
}


public class DeductionPayment : BaseEntity, IEmployeeOwned, IEntryRecord
{
    public Guid EmployeeId { get; set; }
    public Guid DeductionId { get; set; }
    public DateOnly PayrollDate { get; set; }
    public decimal Amount { get; set; }

}