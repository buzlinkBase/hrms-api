using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
using System.ComponentModel.DataAnnotations.Schema;

namespace DTR.Core;

public class ColumnarLogModel
{
    public Guid EmployeeId { get; set; }
    public Guid? ClientId { get; set; }
    public Guid? PayrollGroupId { get; set; }
    public Guid? DepartmentId { get; set; }
    public string EmpNo { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public DateOnly WorkDate { get; set; }
    public string ShiftName { get; set; } = string.Empty;
    public DateTime ShiftStart { get; set; }
    public DateTime ShiftEnd { get; set; }
    public DateTime? BreakOut { get; set; }
    public DateTime? BreakIn { get; set; }

    public AttInfo? Log1 { get; set; }
    public AttInfo? Log2 { get; set; }
    public AttInfo? Log3 { get; set; }
    public AttInfo? Log4 { get; set; }
    public AttInfo? Log5 { get; set; }
    public AttInfo? Log6 { get; set; }
    public AttInfo? Log7 { get; set; }
    public AttInfo? Log8 { get; set; }
    public AttInfo? Log9 { get; set; }
    public AttInfo? Log10 { get; set; }
    public AttInfo? Log11 { get; set; }
    public AttInfo? Log12 { get; set; }
    public AttInfo? Log13 { get; set; }
    public AttInfo? Log14 { get; set; }
    public AttInfo? Log15 { get; set; }
    public AttInfo? Log16 { get; set; }
    public AttInfo? Log17 { get; set; }
    public AttInfo? Log18 { get; set; }
    public AttInfo? Log19 { get; set; }
    public AttInfo? Log20 { get; set; }
}
public class RowLogModel
{
    public Guid EmployeeId { get; set; }
    public string EmpNo { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public DateOnly WorkDate { get; set; }
    public string ShiftName { get; set; } = string.Empty;
    public DateTime ShiftStart { get; set; }
    public DateTime ShiftEnd { get; set; }
    public DateTime? BreakOut { get; set; }
    public DateTime? BreakIn { get; set; }

    public AttInfo? Log1 { get; set; }
}
public class AttInfo
{
    public AttInfo(Guid attId, DateTime workTime)
    {
        AttId = attId;
        WorkTime = workTime;
    }
    public Guid AttId { get; set; }
    public DateTime WorkTime { get; set; }
    public static AttInfo Set(Guid Id, DateTime workTime) => new AttInfo(Id, workTime);
}

public class DTRDetailModel
{
    public string FullName { get; set; } = string.Empty;
    public Guid EmployeeId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? PayrollGroupId { get; set; }
    public Guid? ClientId { get; set; }
    public Guid? AreaId { get; set; }
    public string WorkType { get; set; } = string.Empty;
    public WorkType WorkTypeEnum { get; set; }
    public int Absent { get; set; }
    public int HolCount { get; set; }
    public int SPCount { get; set; }

    // --- Shift & Work Schedule Info ---
    public double ShiftWorkingHour { get; set; }
    public DateOnly WorkDate { get; set; }
    public string ShiftName { get; set; } = string.Empty;
    public DateTime? ShiftStartTime { get; set; } // Or string/DateTime depending on your context
    public DateTime? ShiftEndTime { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }

    // --- Totals in Days ---
    //public double LHHolidayTotalDays { get; set; }
    //public double SPHolidayTotalDays { get; set; }
    //public double RegularWorkingDays { get; set; }
    //public double RegularNDDays { get; set; }
    //public double RegularOTDays { get; set; }
    //public double RegularNDOTDays { get; set; }
    //public double RestDayDays { get; set; }
    //public double RestDayNDDays { get; set; }
    //public double RestDayOTDays { get; set; }
    //public double RestDayNDODays { get; set; }

    // --- Totals in Minutes ---
    public double Late { get; set; }
    public double UT { get; set; }
    public double OverBreak { get; set; }
    //public double OTMinutes { get; set; }
    //public double ND { get; set; }
    //public double NDOT { get; set; }
    //public double LH { get; set; }
    //public double SP { get; set; }
    public double LeaveHours { get; set; }
    public double OBHours { get; set; }

    // Holiday & Night Diff Minutes
    //public double LegalHolOTMinutes { get; set; }
    //public double LegalHolNightDiffMinutes { get; set; }
    //public double LegalHolNightDiffOTMinutes { get; set; }
    //public double SpecialHolOTMinutes { get; set; }
    //public double SpecialHolNightDiffMinutes { get; set; }
    //public double SpecialHolNightDiffOTMinutes { get; set; }
    // Regular & Rest Day Minutes
    //public double RegDayMinutes { get; set; }
    //public double RegDayNDMinutes { get; set; }
    //public double RegDayOTMinutes { get; set; }
    //public double RegDayNDOMinutes { get; set; }
    //public double RestDayMinutes { get; set; }
    //public double RestDayNDMinutes { get; set; }
    //public double RestDayOTMinutes { get; set; }
    //public double RestDayNDOMinutes { get; set; }

    // --- Totals in Hours ---
    public double LateHours { get; set; }
    public double OverBreakHours { get; set; }
    public double LateForOTHours { get; set; }

    // Regular & Rest Day Hours
    public double RegularNetHours { get; set; }
    public double RegularOTHours { get; set; }
    public double RegularNDHours { get; set; }
    public double RegularNDOTHours { get; set; }
    public double RestDayHours { get; set; }
    public double RestDayOTHours { get; set; }
    public double RestDayNDHours { get; set; }
    public double RestDayNDOTHours { get; set; }

    // Holiday Hours
    public double LegalHolHours { get; set; }
    public double LegalHolOTHours { get; set; }
    public double LegalHolNightDiffHours { get; set; }
    public double LegalHolNightDiffOTHours { get; set; }
    public double SpecialHolHours { get; set; }
    public double SpecialHolOTHours { get; set; }
    public double SpecialHolNightDiffHours { get; set; }
    public double SpecialHolNightDiffOTHours { get; set; }

    // Combined Rest + Holiday Hours
    public double RestLegalDayHours { get; set; }
    public double RestLegalDayOTHours { get; set; }
    public double RestLegalDayNDHours { get; set; }
    public double RestLegalDayNDOTHours { get; set; }
    public double RestSpecialDayHours { get; set; }
    public double RestSpecialDayOTHours { get; set; }
    public double RestSpecialDayNDHours { get; set; }
    public double RestSpecialDayNDOTHours { get; set; }
    public double TotalHours => new[]
                {
                    RegularNetHours, RegularOTHours, RegularNDHours, RegularNDOTHours,
                    RestDayHours, RestDayOTHours, RestDayNDHours, RestDayNDOTHours,
                    //RestLegalDayHours, RestLegalDayOTHours, RestLegalDayNDHours, RestLegalDayNDOTHours,
                    //RestSpecialDayHours, RestSpecialDayOTHours, RestSpecialDayNDHours, RestSpecialDayNDOTHours,
                    LegalHolHours, LegalHolOTHours, LegalHolNightDiffHours, LegalHolNightDiffOTHours,
                    SpecialHolHours, SpecialHolOTHours, SpecialHolNightDiffHours, SpecialHolNightDiffOTHours
                }.Sum();
}