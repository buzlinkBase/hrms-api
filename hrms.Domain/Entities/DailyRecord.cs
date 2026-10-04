
using BuzlinkRepository;
using Hrms.Domain.Entities.EmployeeEntities;

namespace Hrms.Domain.Entities;

[DisableSoftDelete]
public class DailyRecord : BaseEntity, IUserField, IEmployeeOwned
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

    // The PayrollBatch that already paid this day, null while unpaid. Day-level (not batch-level)
    // so one employee's days from several DTR batches/clients can be paid across different runs
    // without paying any day twice -- see PayrollProcessorService.GenerateAsync.
    public Guid? PayrollBatchId { get; set; }

    // Worked hours = Hrs + OT of every day type — exactly one WorkType group is ever populated
    // per day (see WorkTypeResolver), so this is a straight sum. ND/ND-OT are not added: the DTR
    // stores natural hours (Hrs and OT already include their night portion), so adding them
    // would double count. Deliberately excludes OBHours/PaidLeaveHours/UnpaidLeaveHours — those
    // aren't worked hours. Mirrors DTRDetailModel.TotalHours; DailyRecord doesn't have the
    // SpecialWorkDay* fields DTRDetailModel does, so they're simply omitted here. Getter-only —
    // EF Core won't map this to a column. Used to pick the hours-bearing record when more than one
    // DailyRecord candidate exists for the same employee+date (see EmployeePayrollLineService).
    public double TotalHours => RegularNetHours + RegularOTHours
        + RestDayHours + RestDayOTHours
        + LegalHolHours + LegalHolOTHours
        + SpecialHolHours + SpecialHolOTHours
        + RestLegalDayHours + RestLegalDayOTHours
        + RestSpecialDayHours + RestSpecialDayOTHours
        + DoubleLegalHours + DoubleLegalOTHours
        + RestDoubleLegalHours + RestDoubleLegalOTHours;
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

