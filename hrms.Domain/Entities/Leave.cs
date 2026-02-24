using Hrms.Domain.Entities.EmployeeEntities;

namespace Hrms.Domain.Entities;

public class Leave : BaseEntity
{
    public string? Category { get; set; } // e.g. Statutory, Company
    public string Code { get; set; } = string.Empty; // e.g. "MAT"
    public string Description { get; set; } = string.Empty; // e.g. "Maternity Leave" 
    public double Credits { get; set; } // default entitlement 
    public PaySource PaySource { get; set; }
    public LeaveReset LeaveReset { get; set; } = LeaveReset.PerPeriod;
    public string Remarks { get; set; } = string.Empty;
}

public class LeaveApplication : BaseEntity
{
    public Guid LeaveId { get; set; }
    public Guid EmployeeId { get; set; }
    public DateOnly LeaveDateFrom { get; set; }
    public DateOnly LeaveDateTo { get; set; }
    public LeaveDayType DayType { get; set; } = LeaveDayType.WholeDay;
    public PayType PayType { get; set; } = PayType.WithPay;
    public ApprovalStatus ApprovalStatus { get; set; }
    public int? ReviewedBy { get; set; }
    public DateTime? ReviewedOn { get; set; }
    public string? ApplicationRemarks { get; set; }
    public int? AuditTrailId { get; set; }
    public virtual ICollection<LeaveApplicationDetail> Details { get; set; } = new List<LeaveApplicationDetail>();
}

public class LeaveApplicationDetail : BaseEntity
{
    public Guid ApplicationId { get; set; }
    public DateOnly LeaveDate { get; set; }
    public virtual LeaveApplication Application { get; set; }
}

public class LeaveCredits : BaseEntity
{
    //validity
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }

    public Guid EmployeeId { get; set; }
    public Guid LeaveId { get; set; }
    public decimal Credits { get; set; }
    public decimal Balance { get; set; }

}

public class LeaveLedger : BaseEntity
{
    public Guid EmployeeId { get; set; }
    public virtual Employee Employee { get; set; }
    public Guid LeaveId { get; set; }
    public DateOnly EntryDate { get; set; }
    public decimal Add { get; set; }
    public decimal Less { get; set; }
    public decimal Balance { get; set; }
    public string Particulars { get; set; } = string.Empty;
    public Guid LeaveCreditsId { get; set; } //  fk for credits
    public Guid? ReferenceApplicationId { get; set; } // link to LeaveApplication

}
