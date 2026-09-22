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

/// <summary>
/// How a FIXED-salary employee's Daily Rate is established.
/// Manual: the stored DailyRate is entered and used as-is.
/// CalculatedEDR: DailyRate = (MonthlyRate * 12) / FactorDays, recomputed by DailyRateResolver.
/// MonthlyTotalDays: DailyRate = MonthlyRate / (actual number of days in the payroll month),
/// so it varies per month (28/29/30/31) instead of using a fixed annual factor.
/// </summary>
public enum DailyRateMode
{
    Manual,
    CalculatedEDR,
    MonthlyTotalDays
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

// Client.UniformAllowanceBasis — how the future Uniform Allowance accrual worker will count an
// employee's elapsed service, mirroring the same two counting methods Leave Types already use
// for eligibility (see LeaveEligibilityBasis below). Kept as its own enum rather than reusing
// LeaveEligibilityBasis directly so Benefits' schema isn't coupled to a type whose name/doc
// comments are scoped to Leave. Only the config field is added for now -- the worker that reads
// this to compute an accruing balance is a later phase.
public enum BenefitAccrualBasis
{
    TenureMonths,  // whole calendar months since Employee.HireDate -- see LeaveEligibilityCalculator.MonthsBetween
    PresentDays,   // count of posted DailyRecord days that aren't Absent/Incomplete/Skipped -- see DailyRecordService.CountPresentDaysAsync
}

public enum ComputationBasis
{
    None,
    FixedPerPayroll,
    Table
}

public enum StatutoryDeductionSchedule
{
    PerPayroll,
    FirstHalfMonth,
    SecondHalfMonth,
}

// Distinguishes a regular DTR-cutoff-driven payroll run from a 13th month pay run — see
// PayrollProcessorService.GenerateThirteenthMonthAsync. Lives on both PayrollBatch (canonical)
// and Payroll (denormalized per-row copy, same pattern as IsPosted) so report queries can
// filter by type without a join.
public enum PayrollType
{
    Regular,
    ThirteenthMonth,
    LastPay,
    YearEndAdjustment,
}

public enum BillingCycle
{
    Monthly,
    SemiMonthly,
    PerCutoff,
}

/// <summary>
/// For a payroll cutoff spanning two calendar months (e.g. Dec 26-Jan 10), which month's
/// remittance the withheld amount is credited against. Shared enum, used by two independent
/// settings: SSS/PhilHealth/Pag-IBIG (CompanyPolicyRule.CrossMonthStatutoryCreditPolicy,
/// default CutoffStartMonth - period-earned convention) and BIR withholding tax
/// (CompanyPolicyRule.WTaxCrossMonthCreditPolicy, default CutoffEndMonth - BIR Form 1601-C
/// reports tax against the month compensation was actually paid/released, not earned).
/// </summary>
public enum CrossMonthStatutoryCreditPolicy
{
    /// <summary>
    /// Credit to the month the cutoff STARTS in. Standard Philippine payroll practice:
    /// contributions are recorded against the month the compensation period covers, so a
    /// cutoff spanning two months is credited to the earlier one. Also matches every
    /// statutory calculator's existing hire-date/proration logic, which already keys off
    /// the cutoff's start date.
    /// </summary>
    CutoffStartMonth,
    /// <summary>
    /// Credit to the month the cutoff ENDS in - i.e. the month payroll is actually
    /// released. For companies that align statutory remittance with the payout date
    /// instead of the period worked.
    /// </summary>
    CutoffEndMonth,
    /// <summary>
    /// Credit to the explicit Pay/Release Date supplied on the payroll run request
    /// (PayrollRunPayload.PayDate) - not derived from the cutoff dates or the system
    /// clock. For companies whose actual payout date routinely differs from the cutoff's
    /// end date (processing lag). Generating payroll without a PayDate while this option
    /// is active fails validation rather than silently falling back to CutoffEndMonth.
    /// </summary>
    PayDate,
}

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
    GovFundedLeave,             // Approved Leave with Pay 
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

public enum PayoutMode
{
    PerDay,   // paid through the normal daily DTR/payroll pipeline
    OneTime   // released as a single lump sum during a specific payroll run — see
              // LeaveApplication.ReleasePayrollDate/GovernmentAmount/CompanyAmount
}

public enum ReimbursementStatus
{
    NotFiled,     // employer has not yet filed the SSS/government reimbursement claim
    Filed,        // claim submitted, awaiting the government's reimbursement
    Reimbursed    // government has paid the employer back
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

public enum LeaveEligibilityBasis
{
    TenureMonths,  // Leave.MinServiceMonths — calendar months since Employee.HireDate
    PresentDays,   // Leave.MinPresentDays — count of posted DailyRecord days that aren't
                   // Absent/Incomplete/Skipped (holidays and rest days, worked or not, count)
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
    HOLIDAY_OT,

    // Setup > Client > Settings > Rate Multipliers — per-client OT PREMIUM overrides for one
    // holiday/rest-day category, compounding with that same category's day-type rate above
    // (LEGAL_HOLIDAY_DUTY, SPECIAL_NON_WORKING, etc.) exactly the way HOLIDAY_OT does — just
    // per-category instead of shared. Client-only by design (never seeded/edited company-wide):
    // CompoundedOtRateStrategy.ResolveRawOtRate falls back to HOLIDAY_OT's own full resolution
    // (client override, else company-wide, else RATE_DEFAULT.HOLIDAY_OT) when a client leaves
    // one of these blank, so "not overridden" means "use the shared Non-Regular OT rate," not a
    // flat/decoupled total the way the old LEGAL_HOLIDAY_OT-shaped types on this enum used to.
    RESTDAY_OT_PREMIUM,
    LEGAL_HOLIDAY_OT_PREMIUM,
    SPECIAL_HOLIDAY_OT_PREMIUM,
    RESTLEGAL_OT_PREMIUM,
    RESTSPECIAL_OT_PREMIUM,
    DOUBLELEGAL_OT_PREMIUM,
    RESTDOUBLELEGAL_OT_PREMIUM,
}

public enum ApprovalStatus
{
    ForApproval,
    Approved,
    Cancelled,
    Declined,
    Withdrawn // employee cancelled their own still-pending (ForApproval) application
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
    WaivePriorDayRequirement,
    CrossMonthStatutoryCreditPolicy,
    WTaxCrossMonthCreditPolicy,
    // Client-only (no company-wide equivalent) — see StatutoryCapHelper.ApplyClientCap. Null or
    // 0 (GeneralSettingsUtil.ParseDecimal's fallback) means no cap, i.e. unchanged behavior.
    MaxSSSCapping,
    MaxPhilHealthCapping,
    MaxPagIbigCapping,
    // Company-wide minimum take-home floor — see DeductionValidator.CanApply. Percentage of
    // gross income (0-100) that scheduled/statutory deductions may never cut into.
    RequiredTakehomePercentage,
    // Company-wide only. Picks how hours that are overtime (OT-only or NDOT) price their day/OT/ND
    // rates — see OvertimeCategoryPolicies.SingleCategoryOTPolicy and
    // NightDiffOTCategoryPolicies.SingleCategoryNDOTPolicy.
    OtNdCalculationMethod,
}

// How OT-involving hours (OT-only or NDOT) price their day/OT/ND rates. Compounded (default,
// DOLE-standard): dayRate x otRate (x ndRate for NDOT). Additive: the day-type multiplier is
// DROPPED entirely for these hours -- only the raw OT rate applies (plus (ndRate - 1) for NDOT),
// i.e. otRate, or otRate + (ndRate - 1). Computes LESS than Compounded for any category whose day
// rate isn't 1.0; company-wide opt-in only, with a compliance warning shown in Setup > Company
// Policy. Confirmed against the company's own manual payroll worksheet. Plain ND-only hours (no
// OT) are unaffected by this setting in either mode -- they always keep the day-type rate.
public enum OtNdCalculationMethod
{
    Compounded,
    Additive,
}

// Which statutory contribution a ClientStatutoryCapKey caps — see StatutoryCapHelper.
public enum StatutoryCapType
{
    SSS,
    PhilHealth,
    PagIbig,
}

public enum IncludeNullResponse
{
    Include,
    Ignore
}

// Which tier of WorkScheduleResolver's priority chain produced an employee's shift for a
// given date: a date-specific Work Rotation Plan override, the day-of-week Fixed Schedule
// default, the employee's Permanent Shift, or no shift configured at all (Open Shift).
public enum ScheduleSource
{
    Override,
    FixedSchedule,
    Permanent,
    OpenShift,
}

// The 6 Applications types the approval-workflow engine covers — see ApprovalWorkflow.
public enum ApprovalApplicationType
{
    Leave,
    Overtime,
    OfficialBusiness,
    PassSlip,
    Loan,
    ProfileUpdate,
}

// Who a workflow step's approver resolves to. Person/Department/Position are fixed at
// configuration time; ApplicantManager/ApplicantDepartment are resolved fresh per applicant at
// approval time (see ApprovalEngineService) and may sit at any step, not just the first.
public enum ApproverType
{
    Person,
    Department,
    Position,
    ApplicantManager,
    ApplicantDepartment,
}

public enum NoteRequirement
{
    None,
    Optional,
    Required,
}

public enum ApprovalInstanceStatus
{
    InProgress,
    Approved,
    Declined,
    Cancelled,
}

public enum ApprovalActionType
{
    Approved,
    Declined,
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

