namespace Hrms.Domain.ValueObjects;

public class DailyRecordRunModel
{
    public string WorkType { get; set; } = "REGULAR WORK DAY";
    public string FullName { get; set; } = string.Empty;
    public Guid EmployeeId { get; set; }
    public string empCode { get; set; } = string.Empty;
    public int BioId { get; set; } = 0;
    public EmployeeModelPayrollRun Employee { get; set; }
    public DateOnly WorkDate { get; set; }
    //minutes regardless if off or not
    public double LateMinutes { get; set; } = 0;
    public double UTMinutes { get; set; } = 0;
    public double OverBreakMinutes { get; set; } = 0;
    ///  

    //hours
    public double LateHours { get; set; }
    //public double UTHours { get; set; }
    public double OverBreakHours { get; set; }
    public double LateForOTHours { get; set; }

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
    public double LeaveMinutes { get; set; }
    public double OB { get; set; }
    public int Absent { get; set; }
    public Guid UserId { get; set; }
    public Guid? ClientId { get; set; }
    public Guid? PayrollGroupId { get; set; }
    public Guid? DepartmentId { get; set; }
    public int HolCount { get; set; } = 0;
    public int SPCount { get; set; } = 0;
    public decimal ShiftWorkingHour { get; set; } = 8;
}