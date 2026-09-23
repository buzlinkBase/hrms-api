
 
namespace Hrms.Domain.ValueObjects;

public class DTRDetailModel
{
    public Guid? Id { get; set; }
    public string? BatchCode { get; set; }
    public string WorkType { get; set; } = string.Empty;
    public WorkType WorkTypeEnum { get; set; }
    public string? FullName { get; set; }
    public Guid? ShiftId { get; set; }
    public Guid EmployeeId { get; set; }
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
    public List<LeaveMetaDataModel>? LeavesInfo { get; set; }
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

    public double SpecialWorkDayHours { get; set; }
    public double SpecialWorkDayOTHours { get; set; }
    public double SpecialWorkDayNDHours { get; set; }
    public double SpecialWorkDayNDOTHours { get; set; }

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
    // Sum of every mutually-exclusive Hrs/OT/ND/ND-OT bucket the DTR detail table displays —
    // exactly one WorkType group (Regular/RestDay/LegalHol/SpecialHol/RestLegal/RestSpecial/
    // DoubleLegal/RestDoubleLegal) is ever populated per day (see WorkTypeResolver), so this is
    // a straight sum, not a max/first. Deliberately excludes OBHours/PaidLeaveHours/
    // UnpaidLeaveHours — those aren't worked hours.
    public double TotalHours => RegularNetHours + RegularOTHours + RegularNDHours + RegularNDOTHours
        + RestDayHours + RestDayOTHours + RestDayNDHours + RestDayNDOTHours
        + LegalHolHours + LegalHolOTHours + LegalHolNightDiffHours + LegalHolNightDiffOTHours
        + SpecialHolHours + SpecialHolOTHours + SpecialHolNightDiffHours + SpecialHolNightDiffOTHours
        + RestLegalDayHours + RestLegalDayOTHours + RestLegalDayNDHours + RestLegalDayNDOTHours
        + RestSpecialDayHours + RestSpecialDayOTHours + RestSpecialDayNDHours + RestSpecialDayNDOTHours
        + DoubleLegalHours + DoubleLegalOTHours + DoubleLegalNDHours + DoubleLegalNDOTHours
        + RestDoubleLegalHours + RestDoubleLegalOTHours + RestDoubleLegalNDHours + RestDoubleLegalNDOTHours
        + SpecialWorkDayHours + SpecialWorkDayOTHours + SpecialWorkDayNDHours + SpecialWorkDayNDOTHours
        ;
}
public class DTRSummaryModel
{
    public string? BatchCode { get; set; }
    public string? FullName { get; set; }
    public Guid EmployeeId { get; set; }
    public double LateHours { get; set; }
    public double UTHours { get; set; }
    public double OverHours { get; set; }
    //public double LateForOTHours { get; set; }
    //public double OBHours { get; set; }
    public int AbsentCount { get; set; }
    //public int HolCount { get; set; } = 0;
    //public int SPCount { get; set; } = 0;
    public double LeaveHours { get; set; }
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

    public double DoubleLegalHours { get; set; }
    public double DoubleLegalOTHours { get; set; }
    public double DoubleLegalNDHours { get; set; }
    public double DoubleLegalNDOTHours { get; set; }
    public double RestDoubleLegalHours { get; set; }
    public double RestDoubleLegalOTHours { get; set; }
    public double RestDoubleLegalNDHours { get; set; }
    public double RestDoubleLegalNDOTHours { get; set; }

}

public class BatchesModel
{
    // The DTRBatch header row's own id -- null for a legacy batch that predates that entity
    // (has DailyRecord rows but no DTRBatch), in which case ApprovalStatus below is reported as
    // Approved (already usable/postable before this feature existed, so it must not retroactively
    // show as pending). See DailyRecordService.GetBatches.
    public Guid? Id { get; set; }
    public string? Code { get; set; }
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public int EmployeeCount { get; set; }
    public bool IsPosted { get; set; }
    public string? PostingDescription { get; set; }
    // True once a payroll has already been generated from this DTR batch — see
    // Payroll.DtrBatchCodes / PayrollService.GetUsedDtrBatchCodesAsync. Used by the
    // Payroll Run screen to block re-selecting a batch that was already posted.
    public bool IsPayrollGenerated { get; set; }
    public ApprovalStatus ApprovalStatus { get; set; }
    public Guid? GeneratedByEmployeeId { get; set; }
    public Guid? PayrollGroupId { get; set; }
    // DTRBatch.CreatedAt -- null for a legacy batch with no DTRBatch row.
    public DateTime? GeneratedAt { get; set; }
    // True while a deletion request on this already-posted batch awaits its own
    // ApprovalApplicationType.DtrDeletion approval -- see DailyRecordService.RequestDeletionAsync.
    // ApprovalStatus stays Approved throughout; this is a separate, orthogonal concern.
    public bool PendingDeletion { get; set; }
    public Guid? RequestedDeletionByEmployeeId { get; set; }
}

public class TardinessReportModel
{
    public DateOnly WorkDate { get; set; }
    public string EmployeeNo { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string? Department { get; set; }
    public DateTime ScheduledIn { get; set; }
    public DateTime? ActualIn { get; set; }
    public double GracePeriodMinutes { get; set; }
    public double TardinessMinutes { get; set; }
    public double DeductibleMinutes { get; set; }
    public bool IsWithinGracePeriod => TardinessMinutes > 0 && DeductibleMinutes == 0;
}

public class RosterReportModel
{
    public DateOnly WorkDate { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeNo { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Department { get; set; }
    public Guid? ShiftId { get; set; }
    public string ShiftName { get; set; } = string.Empty;
    public DateTime? ShiftStart { get; set; }
    public DateTime? ShiftEnd { get; set; }
    public bool IsRestDay { get; set; }
    // Which WorkScheduleResolver tier produced ShiftId — Override, FixedSchedule,
    // Permanent, or OpenShift (see ScheduleSource).
    public ScheduleSource ScheduleSource { get; set; }
    // Set only when the shift shown comes from a Work Rotation Plan override for this
    // exact date — the Id of that override row, deletable via WorkSchedulePlansController.
    // Null when the shift instead resolved from Fixed Schedule or the employee's
    // Permanent Shift, which have no per-date row here to remove.
    public Guid? OverrideId { get; set; }
}

public class LeaveMetaDataModel
{
    public Guid LeaveId { get; set; }
    public string? Name { get; set; } = string.Empty;
    public double Hours { get; set; }
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
    public PayType PayType { get; set; }
}
