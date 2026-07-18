namespace Hrms.Domain;

public enum IncomeClassType
{
    Deminimis,
    Regular,
    Commission,
    Bonus, 
    Reimbursement,  
    Others
}

public enum TimeShiftType
{
    FIXED,
    SPLIT,
    //FLEXI
}

public enum BreakMode
{
    NONE, 
    UNPAID_BREAK,
    PAID_BREAK, 
}
 
public enum SalaryAdjustmentType
{
    Salary,
    Allowance,
    Deduction
}

public enum Period
{
    None,
    Prior,
    Current,
    Both
}

public enum JobLevelOption
{
    //Administrative,
    Managerial,
    Supervisory,
    Executive,
    RankandFile,
    EntryLevel,
    TechnicalSpecialist,
    Contractual,
    FieldStaff
}

public enum PaymentMethod
{
    Cash,
    ATM
}

public enum SalaryType
{
    DAILY,
    MONTHLY_VARIABLE,
    MONTHLY_FIXED
}

public enum PayrollFrequency
{
    DAILY,
    WEEKLY,
    SEMI_MONTHLY,
    MONTHLY
}

public enum DeductionFrequency
{
    Daily,
    Weekly,
    SemiMonthly,
    Monthly,
}
public enum AllowanceFrequency
{
    Daily,
    Weekly,
    SemiMonthly,
    Monthly,
}


public enum ComputationBasis
{
    None,
    FixedPerPayroll,
    FixedMonthly,
    Table
}

//public enum StatutoryDeductionSchedule
//{
//    PerPayroll,
//    FirstHalfMonth,
//    SecondHalfMonth,
//}

public enum EmploymentStatus
{
    Probationary,
    Regular,
    Contractual,
    ProjectBased,
    Seasonal,
    Casual,
    PartTime,
    Term,
    Internship
}

[Flags]
public enum DayName
{
    Sunday, Monday, Tuesday, Wednesday, Thursday, Friday, Saturday
}

public enum ChangeSchedState
{
    DEFAULT,
    OVERRIDEN,
    REPLACEMENT
}

public enum HolidayType
{
    SPECIAL,
    LEGAL,
}
public enum HolidayWorkType
{
    Working,
    NonWorking
}
public enum DayType
{
    RESTDAY,
    REGULAR,
    REGULAR_OVERTIME,
    LEGAL_HOLIDAY_OVERTIME,
    SPECIAL_HOLIDAY_OVERTIME,
    NIGHT_DIFF,
    NONHOLIDAY,
}

//public enum ATT_TYPE
//{
//    REGISTERED,
//    UNREGISTERED
//}

public enum WorkType
{
    RegularWorkDay,
    RestDay,
    RestDayDuty,
    RegularHoliday,
    RegularHolidayDuty,
    PaidLeaveOnLegalHoliday,
    SpecialHoliday,
    SpecialHolidayDutyNW,
    SpecialHolidayDuty,
    RestDayLegalHolidayDuty,
    RestDaySpecialHolidayDuty,
    PaidLeaveOnSpecialHoliday,
    SpecialNonWorking,
    PaidLeave,
    UnpaidLeave,
    Absent,
    Incomplete,
    Skipped,
    PaidLeaveDuty,
    UnpaidLeaveDuty, 
}

public enum DTRStatus
{
    OPEN,
    LOCKED
}
public enum DTRSOURCE
{
    MANUAL,
    SYSTEMCALC,
}

public enum PayType
{
    WithPay,
    WithoutPay
}

public enum LeaveDayType 
{
    WholeDay,
    HalfDay
}
public enum TravelDayType
{
    WholeDay,
    HalfDay
}

public enum LeaveReset
{
    PerEvent,
    PerPeriod,
}

public enum PaySource
{
    Company,       // employer-funded
    Government,    // statutory/SSS-funded
    //Shared,        // employer advances, reimbursed by government
    Unpaid,        // no pay
    Other
}



public enum LeaveType
{
    // Labor Code
    //ServiceIncentive,     // 5 days with pay after 1 year of service
    //Sick,                 // Company-provided, not mandated by law but common
    //Vacation,             // Company-provided, not mandated by law but common
    //// Special Laws
    //Maternity,            // 105 days (with option for extension)
    //Paternity,            // 7 days for married male employees
    //SoloParent,           // 7 days for qualified solo parents
    //Parental,             // Covers parental leave for child care (special cases)
    //SpecialLeaveForWomen, // 2 months for gynecological surgery (RA 9710 Magna Carta of Women)
    //ViolenceAgainstWomen, // 10 days for victims of VAWC (RA 9262)
    //Rehabilitation,       // For employees recovering from work-related injury/illness
    //MagnaCartaDisabled,   // 5 days for employees with disabilities (RA 7277)
    //StudyLeave,           // 2 months for government employees (RA 4670)
    //SpecialEmergency,     // For emergencies/calamities (common in company policies)
    //// Other
    //Unpaid,               // Leave without pay
    //Other                 // Catch-all for company-specific or discretionary leaves
}

public enum RateType
{
    REGULAR,
    NIGHTDIFF,
    OVERTIME,
    RESTDAY_DUTY,
    LEGAL_HOLIDAY,
    LEGAL_HOLIDAY_DUTY,
    SPECIAL_WORKING,
    SPECIAL_NON_WORKING,
    RESTDAY_SPECIAL,
}

public enum ApprovalStatus
{
    ForApproval,
    Approved,
    Cancelled,
    Declined
}




/// Company settings
/// Work Days in a month


//company policy
public enum OvertimeInclusionPolicy
{
    UseEarlyClockIn,        // Includes OT if employee clocks in before scheduled shift
    UsePostShiftWork,       // Includes OT after scheduled shift ends
    UseAllExcessOver8Hours  // Includes any hours beyond 8 as OT, regardless of schedule
}
public enum OvertimeEligibilityRule
{
    RequireFullRegularHours,
    OffsetAgainstUndertimeOrLateness,
    IndependentOfAttendanceIssues,
}
public enum HolidayCreditMode
{
    AutoCredit,
    NoCredit,
}
public enum RecordStatus
{
    Active,
    InActive,
    Any
}

public enum LOGSOURCE
{
    MANUAL,
    UPLOADED,
    BIOMETRIC,
    ADMS,
    GEOFENCE,
    MOBILE,
    OTHER
}
public enum ManualEntryLimitEnum
{
    NOLIMIT,
    DONTALLOW,
    ONE,
    TWO,
    THREE,
    FOUR,
}
public enum HolidayTimeBasis
{
    BasedOnTimeInDayType,// Uses the day type of the employee's time-in (e.g., if they clock in on a holiday)
    BasedOnActualWorkHours // Computes holiday hours based on actual hours worked during the holiday
}

public enum SettingKey
{
    OTEligibility,
    OTInclusion,
    AttFillLimit,
    HolidayTimeBasis,
    IsHalfDayLateOn,
    IsWholeDayLateOn,
    HalfDayLateThresholdMinutes,
    WholeDayLateThresholdMinutes,
    NightDiffThreshold,
    IsHolPlusReg,
    HolidayColumnPresentation,
}

public enum IncludeNullResponse
{
    Include,
    Ignore
}

public enum OutBoxState
{
    PENDING,      // Newly created, awaiting processing
    PROCESSING,   // Currently being handled (optional, useful for concurrency)
    PROCESSED,    // Successfully published
    FAILED,       // Permanent failure, no more retries
    RETRY,        // Temporary failure, will retry
    EXPIRED,      // Timed out, no longer valid (e.g., confirmation tokens)
    CANCELLED,    // Explicitly cancelled/rolled back,
    INVALID//UNKNOWN STATUS
}

