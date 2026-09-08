using Hrms.Domain.Entities.EmployeeEntities;

namespace Hrms.Domain.Entities;

public class ChangeRestDay : BaseEntity
{
    public DayName DayName { get; set; }
    public Guid EmployeeId { get; set; }
    public virtual Employee Employee { get; set; }
    public DateOnly PayrollDate { get; set; }
    public ChangeSchedState State { get; set; }
    public string BatchCode { get; set; }
    // Defaults to Approved so the existing admin batch tool (AddChangeOff) keeps taking
    // immediate effect with no code changes — only the self-service creation path
    // (ChangeRestDayService.RequestChangeOffAsync) explicitly sets ForApproval. See
    // GetChangeRestDays, the only query the DTR RestDayResolver reads through.
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Approved;
}