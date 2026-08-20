namespace Hrms.Domain;

public enum IncomeClassType
{
    Deminimis,
    Regular,
    Commission,
    SpecialBonus,
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
    VARIABLE,
    FIXED
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
    Regular = 1,
    PartTime,
    Probationary,
    Contract,
    Temporary,
    Casual,
    Intern,
    OnLeave,
    Suspended,
    Terminated,
    Resigned,
    Retired,
    Deceased
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
    REGULAR_OT,
    REST_OT, 
    LEGAL,
    LEGAL_OT,
    SPECIAL, 
    SPECIAL_OT,
    NIGHT_DIFF,
    NONHOLIDAY,
    RESTLEGAL,
    RESTLEGAL_OT,
    RESTSPECIAL, 
    RESTSPECIAL_OT,  
    DOUBLE_LEGAL,
    DOUBLE_LEGAL_OT,
    RESTDOUBLE_LEGAL,
    RESTDOUBLE_LEGAL_OT,
}

//public enum ATT_TYPE
//{
//    REGISTERED,
//    UNREGISTERED
//}

public enum WorkType
{
    // 1. Regular Day Columns
    RegularWorkDay,             // Normal scheduled working day (Worked)
    SpecialWorkingHoliday,      // Special Working Holiday (Treated as regular day @ 100%)

    // 2. Rest Day Columns
    RestDay,                    // Scheduled Rest Day (Unworked)
    RestDayDuty,                // Worked on Scheduled Rest Day

    // 3. Legal Holiday Columns
    LegalHoliday,               // Unworked Legal/Regular Holiday
    LegalHolidayDuty,           // Worked on Legal/Regular Holiday

    // 4. Special Holiday Columns (Non-Working)
    SpecialNonWorkingHoliday,   // Unworked Special Non-Working Holiday
    SpecialHolidayDuty,         // Worked on Special Non-Working Holiday

    // 5. Rest + Legal Day Columns
    RestDayLegalHoliday,        // Unworked Legal Holiday that falls on Rest Day
    RestDayLegalHolidayDuty,    // Worked on Legal Holiday that falls on Rest Day

    // 6. Rest + Special Day Columns
    RestDaySpecialHoliday,      // Unworked Special Non-Working Holiday that falls on Rest Day
    RestDaySpecialHolidayDuty,  // Worked on Special Non-Working Holiday that falls on Rest Day

    // Double Legal Holiday (two legal holidays fall on the same day)
    DoubleLegal,                // Unworked double legal holiday (no duty)
    DoubleLegalDuty,            // Worked on double legal holiday
    RestDayDoubleLegal,         // Unworked double legal holiday on rest day
    RestDayDoubleLegalDuty,     // Worked on double legal holiday on rest day

    // 7. Leaves, Attendance & System States
    PaidLeave,                  // Approved Leave with Pay
    UnpaidLeave,                // Approved Leave without Pay
    Absent,                     // Unauthorized Absence / No Show
    Incomplete,                 // Missing Clock In / Clock Out
    Travel,
    RestDayTravel, 
    Skipped                     // Day skipped by processor
}
 
public enum PayType
{
    WithPay,
    WithoutPay
}

public enum DurationType
{
    SingleDay,
    MultiDay,
    Partial
}

public enum DayFraction
{
    FullDay,
    AM,
    PM
}
//public enum TravelDayType
//{
//    WholeDay,
//    HalfDay
//}

public enum LeaveReset
{
    PerEvent,
    PerPeriod,
}

public enum PaySource
{
    Company,       // employer-funded
    Government,    // statutory/SSS-funded
    Shared,        // employer advances, government reimburses (e.g. SSS Maternity)
    Unpaid,        // no pay
    Other
}

public enum AccrualBasis
{
    None,          // manual / lump-sum grant; Credits field is the entitlement
    Monthly,       // AccrualRate days earned each month
    Annually,      // AccrualRate days earned at period start/end
    PerPayPeriod,  // AccrualRate days earned each payroll cycle
    PerEvent,      // full Credits granted on qualifying event (childbirth, illness, etc.)
}

public enum CarryOverType
{
    Forfeit,    // unused balance is lost at period end
    Unlimited,  // entire balance carries over
    Capped,     // up to CarryOverMaxDays carries over; excess forfeited
}

public enum GenderRestriction
{
    None,
    MaleOnly,
    FemaleOnly,
}

public enum RateType
{
    // ── Building-block multipliers (kept for payroll pipeline) ────────────────
    REGULAR,
    NIGHTDIFF,
    OVERTIME,
    RESTDAY_DUTY,
    LEGAL_HOLIDAY,
    LEGAL_HOLIDAY_DUTY,
    SPECIAL_WORKING,
    SPECIAL_NON_WORKING,
    RESTDAY_SPECIAL,
    HOLIDAY_OT,   
    REG,
    REG_OT,
    REG_ND,
    REG_ND_OT,
    // Rest Day
    RD,
    RD_OT,
    RD_ND,
    RD_ND_OT,
    // Legal Holiday (worked)
    LH,
    LH_OT,
    LH_ND,
    LH_ND_OT,
    // Special Non-Working Holiday (worked)
    SH,
    SH_OT,
    SH_ND,
    SH_ND_OT,
    // Rest Day + Legal Holiday
    RD_LH,
    RD_LH_OT,
    RD_LH_ND,
    RD_LH_ND_OT,
    // Rest Day + Special Non-Working Holiday
    RD_SH,
    RD_SH_OT,
    RD_SH_ND,
    RD_SH_ND_OT,
    // Special Working Holiday (same multiplier as regular)
    SW,
    SW_OT,
    SW_ND,
    SW_ND_OT,
    // Double Legal Holiday (worked)
    DLH,
    DLH_OT,
    DLH_ND,
    DLH_ND_OT,
    // Rest Day + Double Legal Holiday
    RD_DLH,
    RD_DLH_OT,
    RD_DLH_ND,
    RD_DLH_ND_OT,
}

public enum ApprovalStatus
{
    ForApproval,
    Approved,
    Cancelled,
    Declined
}

public enum LedgerEntryType
{
    Grant,          // initial credit grant at period start or per-event
    Accrual,        // auto-accrual (monthly / annually / per pay period)
    CarryOver,      // balance rolled over from a previous period
    Reserved,       // Phase 1: soft hold placed on balance when leave is approved
    Released,       // soft hold lifted (cancelled before DTR, or DTR post released delta)
    Deduction,      // Phase 2: DTR-confirmed actual days consumed after batch is posted
    Reversal,       // Deduction undone (leave cancelled/declined after DTR was already posted)
    Expiry,         // balance forfeited at period end (forfeit / cap policy)
    Adjustment,     // manual HR correction
    CashConversion, // balance paid out as cash (monetization)
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
    SYNC,
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
    TimeInAllowance,
    DoublePunchGap,
    CheckAfterHoliday, 
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

