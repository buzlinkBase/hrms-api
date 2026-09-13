
using BuzlinkRepository;
using Hrms.Domain.Entities.EmployeeEntities;

namespace Hrms.Domain.Entities;

[DisableSoftDelete]
public class DailyRecord : BaseEntity, IUserField
{
    public string? BatchCode { get; set; }
    public string WorkType { get; set; } = string.Empty;
    public WorkType WorkTypeEnum { get; set; }
    public string? FullName { get; set; }
    public Guid? ShiftId { get; set; }
    public Guid EmployeeId { get; set; }
    public virtual Employee? Employee { get; set; }
    public virtual List<DtrLeaveMetaData>? LeavesInfo { get; set; }
    public DateOnly WorkDate { get; set; }
    public string ShiftName { get; set; } = string.Empty;
    public DateTime ShiftStartTime { get; set; }
    public DateTime ShiftEndTime { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }

    public double LateMinutes { get; set; }
    public double UTMinutes { get; set; }
    public double OverMinutes { get; set; }
    public double LateForOTMinutes { get; set; }
    public double OBHours { get; set; }
    public int AbsentCount { get; set; }
    public int HolCount { get; set; } = 0;
    public int SPCount { get; set; } = 0;

    public double PaidLeaveHours { get; set; }
    public double UnpaidLeaveHours { get; set; }

    public double RegularNetHours { get; set; }
    public double RegularOTHours { get; set; }
    public double RegularNDHours { get; set; }
    public double RegularNDOTHours { get; set; }

    public double RestDayHours { get; set; }
    public double RestDayOTHours { get; set; }
    public double RestDayNDHours { get; set; }
    public double RestDayNDOTHours { get; set; }

    public double LegalHolHours { get; set; }
    public double LegalHolOTHours { get; set; }
    public double LegalHolNightDiffHours { get; set; }
    public double LegalHolNightDiffOTHours { get; set; }

    public double SpecialHolHours { get; set; }
    public double SpecialHolOTHours { get; set; }
    public double SpecialHolNightDiffHours { get; set; }
    public double SpecialHolNightDiffOTHours { get; set; }

    public double RestLegalDayHours { get; set; }
    public double RestLegalDayOTHours { get; set; }
    public double RestLegalDayNDHours { get; set; }
    public double RestLegalDayNDOTHours { get; set; }

    public double RestSpecialDayHours { get; set; }
    public double RestSpecialDayOTHours { get; set; }
    public double RestSpecialDayNDHours { get; set; }
    public double RestSpecialDayNDOTHours { get; set; }

    //public double SpecialWorkDayHours { get; set; }
    //public double SpecialWorkDayOTHours { get; set; }
    //public double SpecialWorkDayNDHours { get; set; }
    //public double SpecialWorkDayNDOTHours { get; set; }

    public double DoubleLegalHours { get; set; }
    public double DoubleLegalOTHours { get; set; }
    public double DoubleLegalNDHours { get; set; }
    public double DoubleLegalNDOTHours { get; set; }

    public double RestDoubleLegalHours { get; set; }
    public double RestDoubleLegalOTHours { get; set; }
    public double RestDoubleLegalNDHours { get; set; }
    public double RestDoubleLegalNDOTHours { get; set; }

    public string Note { get; set; } = string.Empty;
    public string? PostingDescription { get; set; }

    public Guid? UserId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? PayrollGroupId { get; set; }
    public Guid? ClientId { get; set; }
    public Guid? AreaId { get; set; }
    public double ShiftWorkingHour { get; set; }
    public bool Posted { get; set; }

    // Sum of every mutually-exclusive Hrs/OT/ND/ND-OT bucket below — exactly one WorkType
    // group is ever populated per day (see WorkTypeResolver), so this is a straight sum, not a
    // max/first. Deliberately excludes OBHours/PaidLeaveHours/UnpaidLeaveHours — those aren't
    // worked hours. Mirrors DTRDetailModel.TotalHours (that DTO's own copy of this same
    // calculation); DailyRecord doesn't have the SpecialWorkDay* fields DTRDetailModel does
    // (commented out above), so they're simply omitted here. Getter-only — EF Core won't map
    // this to a column. Used to pick the hours-bearing record when more than one DailyRecord
    // candidate exists for the same employee+date (see EmployeePayrollLineService.CalculateDTRTimePay).
    public double TotalHours => RegularNetHours + RegularOTHours + RegularNDHours + RegularNDOTHours
        + RestDayHours + RestDayOTHours + RestDayNDHours + RestDayNDOTHours
        + LegalHolHours + LegalHolOTHours + LegalHolNightDiffHours + LegalHolNightDiffOTHours
        + SpecialHolHours + SpecialHolOTHours + SpecialHolNightDiffHours + SpecialHolNightDiffOTHours
        + RestLegalDayHours + RestLegalDayOTHours + RestLegalDayNDHours + RestLegalDayNDOTHours
        + RestSpecialDayHours + RestSpecialDayOTHours + RestSpecialDayNDHours + RestSpecialDayNDOTHours
        + DoubleLegalHours + DoubleLegalOTHours + DoubleLegalNDHours + DoubleLegalNDOTHours
        + RestDoubleLegalHours + RestDoubleLegalOTHours + RestDoubleLegalNDHours + RestDoubleLegalNDOTHours;
}

[DisableSoftDelete]
public class DtrLeaveMetaData : BaseEntity
{
    public Guid DTRId  { get; set; }
    public virtual DailyRecord? DTR  { get; set; }
    public Guid LeaveId { get; set; }
    public string? Name { get; set; } = string.Empty;
    public double Hours { get; set; }
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
    public PayType PayType { get; set; }  
}

