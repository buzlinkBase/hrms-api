
using DTR.Core;
using Hrms.Domain.Entities;
using Hrms.Infrastructure.Migrations;
namespace Hrms.Core.Services;

public class AccountInitService : BaseService<Company>
{
    private readonly GeneralSettingService _settingService;
    public AccountInitService(IUnitOfWorkService uow,
        GeneralSettingService settingService) : base(uow)
    {
        _settingService = settingService;
    }

    public async Task Create(CancellationToken token)
    {
        await SetDefaultLeaves(token);
        await SetDefaultRates(token);
        await SetDefaultIncomeTypes(token);
        await SetDefaultDeductionTypes(token);
        await SetBranch(token);
        await SetDefaultHolidays(token);
        await SetDefaultSSSTable(token);
        await SetDefaultPHICTable(token);
        await SetDefaultHDMFTable(token);
        await SetDefaultWTaxTable(token);
        await SetDefaultAnnualTaxTable(token);
        await SetDefaultPayrollGroups(token);
        await SetDefaultTimeShifts(token);
        await PayrollSettings(token);
        await SetDefaultStatutoryCreditPolicy(token);
        await SetDefaultAttendancePayrollPolicy(token);
        //await SetDefaultPayrollInclusionDefaults(token);
    }

    private async Task SetBranch(CancellationToken token)
    {
        var branch = new Branch
        {
            Code = "Main",
            Name = "Main Office",
            Address = "",
            Contact = "",
            ShortName = "Main",
            Email = "",
            ManagerName = ""
        };
        await _uow.Repository.AddAsync(branch, token);
    }

    private async Task PayrollSettings(CancellationToken token)
    {
        var IdentityType = PayrollSettingsIdentity.IdentityType;
        var KeyFiscalYearStartMonth = PayrollSettingsIdentity.KeyFiscalYearStartMonth;
        string KeyThirteenthMonthExemptionCeiling = PayrollSettingsIdentity.KeyThirteenthMonthExemptionCeiling;
        var incoming = new List<GeneralSetting>
        {
            new() { IdentityType = IdentityType, Description = KeyFiscalYearStartMonth, Value = 1.ToString() },
            new() { IdentityType = IdentityType, Description = KeyThirteenthMonthExemptionCeiling, Value = "90000" },
        };
        await _settingService.ReplaceByIdentityTypeAsync(IdentityType, incoming, null, token);
    }

    /// <summary>
    /// Seeds the Company-identity default for the cross-month statutory credit policy so
    /// every new tenant starts with an explicit, editable row (CutoffStartMonth — standard
    /// Philippine payroll practice: contributions credited to the month the cutoff starts)
    /// instead of relying solely on the GeneralSettingsEfConfig migration seed, which is a
    /// single global row and does not reach tenants provisioned after it applies. Uses a
    /// direct insert (not GeneralSettingService.ReplaceByIdentityTypeAsync) because that
    /// method replaces every "Company"-identity row wholesale, and this and
    /// SetDefaultAttendancePayrollPolicy each seed their own slice of Company settings.
    /// </summary>
    private async Task SetDefaultStatutoryCreditPolicy(CancellationToken token)
    {
        var settings = new List<GeneralSetting>
        {
            new()
            {
                Id = Guid.CreateVersion7(),
                IdentityType = "Company",
                Description = SettingKey.CrossMonthStatutoryCreditPolicy.ToString(),
                Value = CrossMonthStatutoryCreditPolicy.CutoffStartMonth.ToString(),
            },
            // WTax defaults to the month the cutoff ENDS in (payout month) — BIR Form
            // 1601-C reports withholding tax against the month compensation was actually
            // paid, unlike SSS/PhilHealth/Pag-IBIG's period-earned convention above.
            new()
            {
                Id = Guid.CreateVersion7(),
                IdentityType = "Company",
                Description = SettingKey.WTaxCrossMonthCreditPolicy.ToString(),
                Value = CrossMonthStatutoryCreditPolicy.CutoffEndMonth.ToString(),
            },
        };
        await _uow.Repository.AddRangeAsync(settings, token);
    }

    /// <summary>
    /// Seeds Company-identity defaults for the Attendance &amp; Payroll Policy tab (OT,
    /// late policy, night diff threshold, attendance-fill and holiday-eligibility rules) so
    /// every new tenant starts with explicit, editable rows instead of relying solely on the
    /// runtime fallback defaults in DTR.Core.CompanyPolicyService — which this mirrors value
    /// for value.
    /// </summary>
    private async Task SetDefaultAttendancePayrollPolicy(CancellationToken token)
    {
        var settings = new List<GeneralSetting>
        {
            new() { Id = Guid.CreateVersion7(), IdentityType = "Company", Description = SettingKey.OTInclusion.ToString(), Value = OvertimeInclusionPolicy.UsePostShiftWork.ToString() },
            new() { Id = Guid.CreateVersion7(), IdentityType = "Company", Description = SettingKey.OTEligibility.ToString(), Value = OvertimeEligibilityRule.IndependentOfAttendanceIssues.ToString() },
            new() { Id = Guid.CreateVersion7(), IdentityType = "Company", Description = SettingKey.AttFillLimit.ToString(), Value = ManualEntryLimitEnum.NOLIMIT.ToString() },
            new() { Id = Guid.CreateVersion7(), IdentityType = "Company", Description = SettingKey.HolidayTimeBasis.ToString(), Value = HolidayTimeBasis.BasedOnTimeInDayType.ToString() },
            new() { Id = Guid.CreateVersion7(), IdentityType = "Company", Description = SettingKey.IsHalfDayLateOn.ToString(), Value = false.ToString() },
            new() { Id = Guid.CreateVersion7(), IdentityType = "Company", Description = SettingKey.HalfDayLateThresholdMinutes.ToString(), Value = 0.ToString() },
            new() { Id = Guid.CreateVersion7(), IdentityType = "Company", Description = SettingKey.IsWholeDayLateOn.ToString(), Value = false.ToString() },
            new() { Id = Guid.CreateVersion7(), IdentityType = "Company", Description = SettingKey.WholeDayLateThresholdMinutes.ToString(), Value = 0.ToString() },
            new() { Id = Guid.CreateVersion7(), IdentityType = "Company", Description = SettingKey.NightDiffThreshold.ToString(), Value = 0.ToString() },
            new() { Id = Guid.CreateVersion7(), IdentityType = "Company", Description = SettingKey.IsHolPlusReg.ToString(), Value = false.ToString() },
            new() { Id = Guid.CreateVersion7(), IdentityType = "Company", Description = SettingKey.TimeInAllowance.ToString(), Value = (-120).ToString() },
            new() { Id = Guid.CreateVersion7(), IdentityType = "Company", Description = SettingKey.DoublePunchGap.ToString(), Value = 2.ToString() },
            new() { Id = Guid.CreateVersion7(), IdentityType = "Company", Description = SettingKey.CheckAfterHoliday.ToString(), Value = false.ToString() },
            new() { Id = Guid.CreateVersion7(), IdentityType = "Company", Description = SettingKey.WaivePriorDayRequirement.ToString(), Value = false.ToString() },
        };
        await _uow.Repository.AddRangeAsync(settings, token);
    }
    private async Task SetDefaultLeaves(CancellationToken token)
    {
        var leaves = new List<Leave>
        {
            // ── Statutory — Company-paid ──────────────────────────────────────────
            new Leave
            {
                Id                       = Guid.CreateVersion7(),
                Code                     = "SIL",
                Category                 = "Statutory",
                Description              = "Service Incentive Leave",
                LegalBasis               = "Labor Code Art. 95 (Service Incentive Leave)",
                PaySource                = PaySource.Company,
                AccrualBasis             = AccrualBasis.Annually,
                Credits                  = 0,
                AccrualRate              = 5,
                LeaveReset               = LeaveReset.PerPeriod,
                MinServiceMonths         = 12,
                AllowHalfDay             = true,
                AllowNegativeBalance     = false,
                RequiresCredits          = true,
                CarryOverType            = CarryOverType.Forfeit,
                ConvertToCash            = true,
                CashConversionRate       = 1.0m,
                MaxCashConversionDays    = 5,
                IsStatutory              = true,
                RequiresApproval         = true,
            },
            new Leave
            {
                Id                       = Guid.CreateVersion7(),
                Code                     = "VL",
                Category                 = "Benefit",
                Description              = "Vacation Leave",
                LegalBasis               = string.Empty,
                PaySource                = PaySource.Company,
                AccrualBasis             = AccrualBasis.Monthly,
                Credits                  = 0,
                AccrualRate              = 0.833,
                LeaveReset               = LeaveReset.PerPeriod,
                MinServiceMonths         = 6,
                AllowHalfDay             = true,
                AllowNegativeBalance     = false,
                RequiresCredits          = true,
                CarryOverType            = CarryOverType.Capped,
                CarryOverMaxDays         = 5,
                RequiresApproval         = true,
            },
            new Leave
            {
                Id                       = Guid.CreateVersion7(),
                Code                     = "SL",
                Category                 = "Benefit",
                Description              = "Sick Leave",
                LegalBasis               = string.Empty,
                PaySource                = PaySource.Company,
                AccrualBasis             = AccrualBasis.Monthly,
                Credits                  = 0,
                AccrualRate              = 0.417,
                LeaveReset               = LeaveReset.PerPeriod,
                MinServiceMonths         = 6,
                AllowHalfDay             = true,
                AllowNegativeBalance     = false,
                RequiresCredits          = true,
                CarryOverType            = CarryOverType.Forfeit,
                RequiresApproval         = true,
            },
            new Leave
            {
                Id                       = Guid.CreateVersion7(),
                Code                     = "PL",
                Category                 = "Statutory",
                Description              = "Paternity Leave",
                LegalBasis               = "RA 8187 (Paternity Leave Act of 1996)",
                PaySource                = PaySource.Company,
                AccrualBasis             = AccrualBasis.PerEvent,
                Credits                  = 7,
                LeaveReset               = LeaveReset.PerEvent,
                GenderRestriction        = GenderRestriction.MaleOnly,
                AllowHalfDay             = false,
                AllowNegativeBalance     = false,
                RequiresCredits          = true,
                MaxConsecutiveDays       = 7,
                RequiresSupportingDocument = true,
                RequiresApproval         = true,
                IsStatutory              = true,
            },
            new Leave
            {
                Id                       = Guid.CreateVersion7(),
                Code                     = "SPL",
                Category                 = "Statutory",
                Description              = "Parental Leave for Solo Parents",
                LegalBasis               = "RA 8972 (Solo Parents' Welfare Act of 2000)",
                PaySource                = PaySource.Company,
                AccrualBasis             = AccrualBasis.None,
                Credits                  = 7,
                LeaveReset               = LeaveReset.PerPeriod,
                MinServiceMonths         = 12,
                AllowHalfDay             = true,
                AllowNegativeBalance     = false,
                RequiresCredits          = true,
                CarryOverType            = CarryOverType.Forfeit,
                RequiresSupportingDocument = true,
                RequiresApproval         = true,
                IsStatutory              = true,
            },
            new Leave
            {
                Id                       = Guid.CreateVersion7(),
                Code                     = "SLW",
                Category                 = "Statutory",
                Description              = "Special Leave for Women (Gynecological Disorders)",
                LegalBasis               = "RA 9710 (Magna Carta of Women)",
                PaySource                = PaySource.Company,
                AccrualBasis             = AccrualBasis.PerEvent,
                Credits                  = 60,
                LeaveReset               = LeaveReset.PerEvent,
                GenderRestriction        = GenderRestriction.FemaleOnly,
                MinServiceMonths         = 6,
                AllowHalfDay             = false,
                AllowNegativeBalance     = false,
                RequiresCredits          = true,
                MaxConsecutiveDays       = 60,
                RequiresSupportingDocument = true,
                RequiresApproval         = true,
                IsStatutory              = true,
            },
            new Leave
            {
                Id                       = Guid.CreateVersion7(),
                Code                     = "VAWC",
                Category                 = "Statutory",
                Description              = "Leave for Victims of Violence Against Women and Children",
                LegalBasis               = "RA 9262 (Anti-VAWC Act of 2004)",
                PaySource                = PaySource.Company,
                AccrualBasis             = AccrualBasis.None,
                Credits                  = 10,
                LeaveReset               = LeaveReset.PerPeriod,
                GenderRestriction        = GenderRestriction.FemaleOnly,
                AllowHalfDay             = true,
                AllowNegativeBalance     = false,
                RequiresCredits          = true,
                CarryOverType            = CarryOverType.Forfeit,
                RequiresSupportingDocument = true,
                RequiresApproval         = true,
                IsStatutory              = true,
            },
            new Leave
            {
                Id                       = Guid.CreateVersion7(),
                Code                     = "SEL",
                Category                 = "Emergency",
                Description              = "Special Emergency Leave (Calamities)",
                LegalBasis               = "DOLE Department Order No. 53-03",
                PaySource                = PaySource.Company,
                AccrualBasis             = AccrualBasis.PerEvent,
                Credits                  = 5,
                LeaveReset               = LeaveReset.PerEvent,
                AllowHalfDay             = true,
                AllowNegativeBalance     = false,
                RequiresCredits          = true,
                RequiresApproval         = true,
                IsStatutory              = false,
            },

            // ── Statutory — Shared/Government-paid ───────────────────────────────
            new Leave
            {
                Id                       = Guid.CreateVersion7(),
                Code                     = "ML",
                Category                 = "Statutory",
                Description              = "Maternity Leave",
                LegalBasis               = "RA 11210 (105-Day Expanded Maternity Leave Law)",
                PaySource                = PaySource.Government,
                EmployerAdvancesPayment  = true,
                AccrualBasis             = AccrualBasis.PerEvent,
                Credits                  = 105,
                LeaveReset               = LeaveReset.PerEvent,
                GenderRestriction        = GenderRestriction.FemaleOnly,
                MinServiceMonths         = 3,
                AllowHalfDay             = false,
                AllowNegativeBalance     = false,
                RequiresCredits          = false,
                MaxConsecutiveDays       = 105,
                RequiresSupportingDocument = true,
                RequiresApproval         = true,
                IsStatutory              = true,
            },
            new Leave
            {
                Id                       = Guid.CreateVersion7(),
                Code                     = "RL",
                Category                 = "Statutory",
                Description              = "Rehabilitation Leave (Occupational Injuries)",
                LegalBasis               = "PD 626 (Employees' Compensation Law)",
                PaySource                = PaySource.Shared,
                EmployerAdvancesPayment  = true,
                AccrualBasis             = AccrualBasis.PerEvent,
                Credits                  = 120,
                LeaveReset               = LeaveReset.PerEvent,
                AllowHalfDay             = false,
                AllowNegativeBalance     = false,
                RequiresCredits          = false,
                MaxConsecutiveDays       = 120,
                RequiresSupportingDocument = true,
                RequiresApproval         = true,
                IsStatutory              = true,
            },
            // Distinct from "SL" (company Sick Leave) on purpose — filed once an employee's
            // SL credits are exhausted and the illness continues, so it needs its own credit
            // pool (SL's AllowNegativeBalance=false would otherwise reject the filing). Its
            // PayoutMode is set per-application (see LeaveApplication.PayoutMode) — OneTime
            // when SSS releases it as a lump sum split into Government/Company amounts.
            new Leave
            {
                Id                       = Guid.CreateVersion7(),
                Code                     = "SB",
                Category                 = "Statutory",
                Description              = "SSS Sickness Benefit",
                LegalBasis               = "RA 11199 (Social Security Act of 2018), Sec. 14",
                PaySource                = PaySource.Shared,
                EmployerAdvancesPayment  = true,
                AccrualBasis             = AccrualBasis.None,
                Credits                  = 120,
                LeaveReset               = LeaveReset.PerPeriod,
                AllowHalfDay             = false,
                AllowNegativeBalance     = false,
                RequiresCredits          = false,
                MaxConsecutiveDays       = 120,
                RequiresSupportingDocument = true,
                RequiresApproval         = true,
                IsStatutory              = true,
            },

            // ── Government-sector only ────────────────────────────────────────────
            new Leave
            {
                Id                       = Guid.CreateVersion7(),
                Code                     = "EL",
                Category                 = "Government",
                Description              = "Educational Leave",
                LegalBasis               = "CSC Rules on Leave (Government Employees)",
                PaySource                = PaySource.Government,
                AccrualBasis             = AccrualBasis.None,
                Credits                  = 10,
                LeaveReset               = LeaveReset.PerPeriod,
                AllowHalfDay             = true,
                AllowNegativeBalance     = false,
                RequiresCredits          = false,
                CarryOverType            = CarryOverType.Forfeit,
                RequiresSupportingDocument = true,
                RequiresApproval         = true,
                IsStatutory              = true,
            },
            new Leave
            {
                Id                       = Guid.CreateVersion7(),
                Code                     = "MCL",
                Category                 = "Government",
                Description              = "Magna Carta Leave (Government Employees)",
                LegalBasis               = "RA 7877 / CSC MC 25 s.1994 (Magna Carta for Public School Teachers)",
                PaySource                = PaySource.Government,
                AccrualBasis             = AccrualBasis.None,
                Credits                  = 5,
                LeaveReset               = LeaveReset.PerPeriod,
                AllowHalfDay             = true,
                AllowNegativeBalance     = false,
                RequiresCredits          = false,
                CarryOverType            = CarryOverType.Forfeit,
                RequiresSupportingDocument = false,
                RequiresApproval         = true,
                IsStatutory              = true,
            },
        };
        await _uow.Repository.AddRangeAsync(leaves, token);
    }
    private async Task SetDefaultRates(CancellationToken token)
    {
        var rates = new List<RateTable>
        {
            // ── Building-block multipliers ────────────────────────────────────────
            new RateTable { Id = Guid.CreateVersion7(), Type = RateType.REGULAR,            ShortDescription = "REGULAR",            Description = "Regular",                      Rate = RATE_DEFAULT.REGULAR },
            new RateTable { Id = Guid.CreateVersion7(), Type = RateType.NIGHTDIFF,          ShortDescription = "NIGHTDIFF",          Description = "Night Differential",           Rate = RATE_DEFAULT.NIGHTDIFF },
            new RateTable { Id = Guid.CreateVersion7(), Type = RateType.OVERTIME,           ShortDescription = "OVERTIME",           Description = "Overtime",                     Rate = RATE_DEFAULT.OVERTIME },
            new RateTable { Id = Guid.CreateVersion7(), Type = RateType.RESTDAY_DUTY,       ShortDescription = "RESTDAY_DUTY",       Description = "Rest Day Duty",                Rate = RATE_DEFAULT.RESTDAY_DUTY },
            new RateTable { Id = Guid.CreateVersion7(), Type = RateType.LEGAL_HOLIDAY,      ShortDescription = "LEGAL_HOLIDAY",      Description = "Legal Holiday (No Work)",      Rate = RATE_DEFAULT.LEGAL_HOLIDAY },
            new RateTable { Id = Guid.CreateVersion7(), Type = RateType.LEGAL_HOLIDAY_DUTY, ShortDescription = "LEGAL_HOLIDAY_DUTY", Description = "Legal Holiday (Worked)",       Rate = RATE_DEFAULT.LEGAL_HOLIDAY_DUTY },
            new RateTable { Id = Guid.CreateVersion7(), Type = RateType.SPECIAL_WORKING,    ShortDescription = "SPECIAL_WORKING",    Description = "Special Working Holiday",      Rate = RATE_DEFAULT.SPECIAL_WORKING },
            new RateTable { Id = Guid.CreateVersion7(), Type = RateType.SPECIAL_NON_WORKING,ShortDescription = "SPECIAL_NON_WORKING",Description = "Special Non-Working Holiday",  Rate = RATE_DEFAULT.SPECIAL_NON_WORKING },
            new RateTable { Id = Guid.CreateVersion7(), Type = RateType.RESTDAY_SPECIAL,    ShortDescription = "RESTDAY_SPECIAL",    Description = "Rest Day + Special Holiday",   Rate = RATE_DEFAULT.RESTDAY_SPECIAL },
            new RateTable { Id = Guid.CreateVersion7(), Type = RateType.HOLIDAY_OT,         ShortDescription = "HOLIDAY_OT",         Description = "Holiday / Rest Day OT Premium", Rate = RATE_DEFAULT.HOLIDAY_OT},
        };
        await _uow.Repository.AddRangeAsync(rates, token);
    }
    private async Task SetDefaultIncomeTypes(CancellationToken token)
    {
        var types = new List<OtherIncomeType>
        {
            new OtherIncomeType { Id = Guid.CreateVersion7(), Description = "Allowance" },
            new OtherIncomeType { Id = Guid.CreateVersion7(), Description = "13th Month Pay" },
            new OtherIncomeType { Id = Guid.CreateVersion7(), Description = "Performance Bonus" },
            new OtherIncomeType { Id = Guid.CreateVersion7(), Description = "Meal Allowance" },
            new OtherIncomeType { Id = Guid.CreateVersion7(), Description = "Transportation Allowance" },
            new OtherIncomeType { Id = Guid.CreateVersion7(), Description = "Communication Allowance" },
            new OtherIncomeType { Id = Guid.CreateVersion7(), Description = "Clothing Allowance" },
            new OtherIncomeType { Id = Guid.CreateVersion7(), Description = "Rice Subsidy" },
            new OtherIncomeType { Id = Guid.CreateVersion7(), Description = "Medical / Health Subsidy" },
            new OtherIncomeType { Id = Guid.CreateVersion7(), Description = "Housing Allowance" },
            new OtherIncomeType { Id = Guid.CreateVersion7(), Description = "Laundry Allowance" },
            new OtherIncomeType { Id = Guid.CreateVersion7(), Description = "Uniform Allowance" },
            new OtherIncomeType { Id = Guid.CreateVersion7(), Description = "Emergency Relief" },
            new OtherIncomeType { Id = Guid.CreateVersion7(), Description = "Service Incentive Leave (Cash)" },
        };
        await _uow.Repository.AddRangeAsync(types, token);
    }
    /// <summary>
    /// Seeds the common Philippine holiday calendar. Fixed-date national holidays are
    /// IsRecuring = true (HolidayService.GetAllHolidays substitutes in the current year
    /// automatically, only Month/Day of HolDate matter for those). National Heroes Day is
    /// legally defined as "last Monday of August" (RA 9492), so it's IsRecuring = true with
    /// WeekOfMonth/DayOfWeek set instead — HolidayRecurrenceCalculator.ResolveNthWeekday
    /// recomputes its actual date every year from HolDate's Month plus those two fields, so
    /// only the Month portion of its seeded HolDate matters going forward. True movable
    /// holidays with no nth-weekday rule (Holy Week, Chinese New Year — both follow a
    /// liturgical/lunar calendar) have no fixed month/day at all, so they're seeded with an
    /// actual 2026 date as a starting point and IsRecuring = false — the tenant needs to
    /// update those annually.
    /// </summary>
    private async Task SetDefaultHolidays(CancellationToken token)
    {
        var holidays = new List<Holiday>
        {
            // ── Regular (Legal) Holidays — fixed date, paid whether worked or not ──
            new Holiday { Id = Guid.CreateVersion7(), Description = "New Year's Day", HolType = HolidayType.LEGAL, WorkType = HolidayWorkType.NonWorking, HolDate = new DateOnly(2026, 1, 1), HolYear = 2026, IsRecuring = true, IsPaid = true },
            new Holiday { Id = Guid.CreateVersion7(), Description = "Araw ng Kagitingan (Day of Valor)", HolType = HolidayType.LEGAL, WorkType = HolidayWorkType.NonWorking, HolDate = new DateOnly(2026, 4, 9), HolYear = 2026, IsRecuring = true, IsPaid = true },
            new Holiday { Id = Guid.CreateVersion7(), Description = "Labor Day", HolType = HolidayType.LEGAL, WorkType = HolidayWorkType.NonWorking, HolDate = new DateOnly(2026, 5, 1), HolYear = 2026, IsRecuring = true, IsPaid = true },
            new Holiday { Id = Guid.CreateVersion7(), Description = "Independence Day", HolType = HolidayType.LEGAL, WorkType = HolidayWorkType.NonWorking, HolDate = new DateOnly(2026, 6, 12), HolYear = 2026, IsRecuring = true, IsPaid = true },
            new Holiday { Id = Guid.CreateVersion7(), Description = "Bonifacio Day", HolType = HolidayType.LEGAL, WorkType = HolidayWorkType.NonWorking, HolDate = new DateOnly(2026, 11, 30), HolYear = 2026, IsRecuring = true, IsPaid = true },
            new Holiday { Id = Guid.CreateVersion7(), Description = "Christmas Day", HolType = HolidayType.LEGAL, WorkType = HolidayWorkType.NonWorking, HolDate = new DateOnly(2026, 12, 25), HolYear = 2026, IsRecuring = true, IsPaid = true },
            new Holiday { Id = Guid.CreateVersion7(), Description = "Rizal Day", HolType = HolidayType.LEGAL, WorkType = HolidayWorkType.NonWorking, HolDate = new DateOnly(2026, 12, 30), HolYear = 2026, IsRecuring = true, IsPaid = true },

            // ── Regular (Legal) Holidays — recurring nth-weekday-of-month rule (RA 9492) ──
            new Holiday { Id = Guid.CreateVersion7(), Description = "National Heroes Day", HolType = HolidayType.LEGAL, WorkType = HolidayWorkType.NonWorking, HolDate = new DateOnly(2026, 8, 31), HolYear = 2026, IsRecuring = true, WeekOfMonth = 5, DayOfWeek = DayOfWeek.Monday, IsPaid = true },

            // ── Regular (Legal) Holidays — movable, seeded for 2026, review yearly ──
            new Holiday { Id = Guid.CreateVersion7(), Description = "Maundy Thursday", HolType = HolidayType.LEGAL, WorkType = HolidayWorkType.NonWorking, HolDate = new DateOnly(2026, 4, 2), HolYear = 2026, IsRecuring = false, IsPaid = true },
            new Holiday { Id = Guid.CreateVersion7(), Description = "Good Friday", HolType = HolidayType.LEGAL, WorkType = HolidayWorkType.NonWorking, HolDate = new DateOnly(2026, 4, 3), HolYear = 2026, IsRecuring = false, IsPaid = true },

            // ── Special (Non-Working) Holidays — fixed date, no work no pay by default ──
            new Holiday { Id = Guid.CreateVersion7(), Description = "EDSA People Power Anniversary", HolType = HolidayType.SPECIAL, WorkType = HolidayWorkType.NonWorking, HolDate = new DateOnly(2026, 2, 25), HolYear = 2026, IsRecuring = true, IsPaid = false },
            new Holiday { Id = Guid.CreateVersion7(), Description = "Ninoy Aquino Day", HolType = HolidayType.SPECIAL, WorkType = HolidayWorkType.NonWorking, HolDate = new DateOnly(2026, 8, 21), HolYear = 2026, IsRecuring = true, IsPaid = false },
            new Holiday { Id = Guid.CreateVersion7(), Description = "All Saints' Day", HolType = HolidayType.SPECIAL, WorkType = HolidayWorkType.NonWorking, HolDate = new DateOnly(2026, 11, 1), HolYear = 2026, IsRecuring = true, IsPaid = false },
            new Holiday { Id = Guid.CreateVersion7(), Description = "All Souls' Day", HolType = HolidayType.SPECIAL, WorkType = HolidayWorkType.NonWorking, HolDate = new DateOnly(2026, 11, 2), HolYear = 2026, IsRecuring = true, IsPaid = false },
            new Holiday { Id = Guid.CreateVersion7(), Description = "Feast of the Immaculate Conception", HolType = HolidayType.SPECIAL, WorkType = HolidayWorkType.NonWorking, HolDate = new DateOnly(2026, 12, 8), HolYear = 2026, IsRecuring = true, IsPaid = false },
            new Holiday { Id = Guid.CreateVersion7(), Description = "Christmas Eve", HolType = HolidayType.SPECIAL, WorkType = HolidayWorkType.NonWorking, HolDate = new DateOnly(2026, 12, 24), HolYear = 2026, IsRecuring = true, IsPaid = false },
            new Holiday { Id = Guid.CreateVersion7(), Description = "Last Day of the Year", HolType = HolidayType.SPECIAL, WorkType = HolidayWorkType.NonWorking, HolDate = new DateOnly(2026, 12, 31), HolYear = 2026, IsRecuring = true, IsPaid = false },

            // ── Special (Non-Working) Holidays — movable, seeded for 2026, review yearly ──
            new Holiday { Id = Guid.CreateVersion7(), Description = "Chinese New Year", HolType = HolidayType.SPECIAL, WorkType = HolidayWorkType.NonWorking, HolDate = new DateOnly(2026, 2, 17), HolYear = 2026, IsRecuring = false, IsPaid = false },
            new Holiday { Id = Guid.CreateVersion7(), Description = "Black Saturday", HolType = HolidayType.SPECIAL, WorkType = HolidayWorkType.NonWorking, HolDate = new DateOnly(2026, 4, 4), HolYear = 2026, IsRecuring = false, IsPaid = false },
        };
        await _uow.Repository.AddRangeAsync(holidays, token);
    }

    /// <summary>
    /// Seeds the 2026 SSS contribution schedule (effective 2025 under RA 11199's final
    /// scheduled step, still current as of 2026 — no new schedule has been issued). MSC
    /// brackets step by ₱500 from ₱5,000 to ₱35,000. EE = 5% of MSC, ER = 10% of MSC
    /// (regular SS share), EC (Employees' Compensation, employer-only) = ₱10 for MSC below
    /// ₱15,000 or ₱30 from ₱15,000 up. Review/adjust via Setup → SSS Table if SSS revises
    /// the schedule.
    /// </summary>
    private async Task SetDefaultSSSTable(CancellationToken token)
    {
        var effectiveDate = new DateOnly(2026, 1, 1);
        var table = new List<SSSTable>();
        decimal floor = 0m;
        for (decimal msc = 5000m; msc <= 35000m; msc += 500m)
        {
            var isLast = msc == 35000m;
            var ceiling = isLast ? 9_999_999.99m : msc + 249.99m;
            var ee = Math.Round(msc * 0.05m, 2);
            var er = Math.Round(msc * 0.10m, 2);
            var ec = msc < 15000m ? 10m : 30m;
            table.Add(new SSSTable
            {
                Id = Guid.CreateVersion7(),
                EffectiveDate = effectiveDate,
                RangeFrom = floor,
                RangeTo = ceiling,
                MSC = msc,
                EE = ee,
                ER = er,
                EC = ec,
                TotalContibution = ee + er + ec,
            });
            floor = ceiling + 0.01m;
        }
        await _uow.Repository.AddRangeAsync(table, token);
    }

    /// <summary>
    /// Seeds the 2026 PhilHealth premium schedule (5% of monthly basic salary, floor
    /// ₱10,000 / ceiling ₱100,000, split 2.5% EE / 2.5% ER — the final scheduled step under
    /// RA 11223, the Universal Health Care Act). The floor and ceiling brackets are exact;
    /// the variable middle range is discretized into ₱2,000-wide sub-brackets (rate applied
    /// at each bracket's floor) since PHICTable/PHICHelper match one flat EE/ER per bracket
    /// rather than computing a live percentage — a standard "table" approximation, fine by
    /// default but review boundary values if precision to the centavo matters.
    /// </summary>
    private async Task SetDefaultPHICTable(CancellationToken token)
    {
        var effectiveDate = new DateOnly(2026, 1, 1);
        const decimal rate = 0.05m;
        var table = new List<PHICTable>
        {
            new PHICTable
            {
                Id = Guid.CreateVersion7(), EffectiveDate = effectiveDate,
                MinSalaryBase = 0m, MaxSalaryBase = 10000m, PremiumRate = rate,
                EmployeeShare = 250m, EmployerShare = 250m, TotalContribution = 500m,
                Remarks = "Floor — salary ≤ ₱10,000",
            },
        };

        for (decimal floor = 10000.01m; floor < 100000m;)
        {
            var stepEnd = floor + 2000m;
            var ceiling = stepEnd >= 100000m ? 100000m - 0.01m : stepEnd - 0.01m;
            var premium = Math.Round(floor * rate, 2);
            var share = Math.Round(premium / 2m, 2);
            table.Add(new PHICTable
            {
                Id = Guid.CreateVersion7(),
                EffectiveDate = effectiveDate,
                MinSalaryBase = floor,
                MaxSalaryBase = ceiling,
                PremiumRate = rate,
                EmployeeShare = share,
                EmployerShare = share,
                TotalContribution = share * 2m,
            });
            floor = ceiling + 0.01m;
        }

        table.Add(new PHICTable
        {
            Id = Guid.CreateVersion7(),
            EffectiveDate = effectiveDate,
            MinSalaryBase = 100000m,
            MaxSalaryBase = 9_999_999.99m,
            PremiumRate = rate,
            EmployeeShare = 2500m,
            EmployerShare = 2500m,
            TotalContribution = 5000m,
            Remarks = "Ceiling — salary ≥ ₱100,000",
        });

        await _uow.Repository.AddRangeAsync(table, token);
    }

    /// <summary>
    /// Seeds the 2026 Pag-IBIG (HDMF) contribution schedule: 1% EE / 2% ER for compensation
    /// ≤ ₱1,500, 2% EE / 2% ER above that, capped at the ₱10,000 Maximum Fund Salary (MFS)
    /// effective February 2024 — max ₱200 EE / ₱200 ER, unchanged for 2026. The variable
    /// ranges are discretized into sub-brackets (₱250 below ₱1,500, ₱500 from ₱1,500 to
    /// ₱10,000) for the same reason as PHIC above.
    /// </summary>
    private async Task SetDefaultHDMFTable(CancellationToken token)
    {
        var effectiveDate = new DateOnly(2026, 1, 1);
        var table = new List<HDMFTable>();

        for (decimal floor = 0m; floor < 1500m;)
        {
            var stepEnd = floor + 250m;
            var ceiling = stepEnd >= 1500m ? 1500m : stepEnd - 0.01m;
            var ee = Math.Round(floor * 0.01m, 2);
            var er = Math.Round(floor * 0.02m, 2);
            table.Add(new HDMFTable
            {
                Id = Guid.CreateVersion7(),
                EffectiveDate = effectiveDate,
                MinSalaryBase = floor,
                MaxSalaryBase = ceiling,
                EmployeeRate = 0.01m,
                EmployerRate = 0.02m,
                EmployeeShare = ee,
                EmployerShare = er,
                TotalContribution = ee + er,
            });
            floor = ceiling == 1500m ? 1500.01m : stepEnd;
        }

        for (decimal floor = 1500.01m; floor < 10000m;)
        {
            var stepEnd = floor + 500m;
            var ceiling = stepEnd >= 10000m ? 10000m - 0.01m : stepEnd - 0.01m;
            var ee = Math.Round(floor * 0.02m, 2);
            var er = Math.Round(floor * 0.02m, 2);
            table.Add(new HDMFTable
            {
                Id = Guid.CreateVersion7(),
                EffectiveDate = effectiveDate,
                MinSalaryBase = floor,
                MaxSalaryBase = ceiling,
                EmployeeRate = 0.02m,
                EmployerRate = 0.02m,
                EmployeeShare = ee,
                EmployerShare = er,
                TotalContribution = ee + er,
            });
            floor = ceiling + 0.01m;
        }

        table.Add(new HDMFTable
        {
            Id = Guid.CreateVersion7(),
            EffectiveDate = effectiveDate,
            MinSalaryBase = 10000m,
            MaxSalaryBase = 9_999_999.99m,
            EmployeeRate = 0.02m,
            EmployerRate = 0.02m,
            EmployeeShare = 200m,
            EmployerShare = 200m,
            TotalContribution = 400m,
            Remarks = "Capped at Maximum Fund Salary (MFS) ₱10,000",
        });

        await _uow.Repository.AddRangeAsync(table, token);
    }

    /// <summary>
    /// Seeds the BIR withholding tax tables (Revised Withholding Tax Table, RR 11-2018
    /// Annex E, implementing the TRAIN Law's second-phase rates — effective Jan 1, 2023
    /// and unchanged through 2026) for all four PayrollFrequency variants: Daily, Weekly,
    /// Semi-Monthly, and Monthly.
    /// </summary>
    private async Task SetDefaultWTaxTable(CancellationToken token)
    {
        var effectiveDate = new DateOnly(2026, 1, 1);
        var table = new List<TaxTable>();

        // Each frequency's official BIR table (RR 11-2018 Annex E) has 6 brackets —
        // 0% / 15% / 20% / 25% / 30% / 35%. There is no 32% bracket in the current
        // (2023–2026) schedule; that rate belonged to the superseded 2018–2022 table.
        void AddBracket(string freq, decimal from, decimal to, decimal baseDue, decimal addOn) =>
            table.Add(new TaxTable
            {
                Id = Guid.CreateVersion7(),
                EffectiveDate = effectiveDate,
                PayrollType = freq,
                RangeFrom = from,
                RangeTo = to,
                PercentageInAmountOf = from,
                BaseTaxDue = baseDue,
                AddOnPercentage = addOn,
            });

        var daily = PayrollFrequency.DAILY.ToString();
        AddBracket(daily, 0m, 685m, 0m, 0m);
        AddBracket(daily, 685m, 1095m, 0m, 0.15m);
        AddBracket(daily, 1096m, 2191m, 61.65m, 0.20m);
        AddBracket(daily, 2192m, 5478m, 280.85m, 0.25m);
        AddBracket(daily, 5479m, 21917m, 1102.60m, 0.30m);
        AddBracket(daily, 21918m, 9_999_999.99m, 6034.30m, 0.35m);

        var weekly = PayrollFrequency.WEEKLY.ToString();
        AddBracket(weekly, 0m, 4808m, 0m, 0m);
        AddBracket(weekly, 4808m, 7691m, 0m, 0.15m);
        AddBracket(weekly, 7692m, 15384m, 432.60m, 0.20m);
        AddBracket(weekly, 15385m, 38461m, 1971.20m, 0.25m);
        AddBracket(weekly, 38462m, 153845m, 7740.45m, 0.30m);
        AddBracket(weekly, 153846m, 9_999_999.99m, 42355.65m, 0.35m);

        var semiMonthly = PayrollFrequency.SEMI_MONTHLY.ToString();
        AddBracket(semiMonthly, 0m, 10417m, 0m, 0m);
        AddBracket(semiMonthly, 10417m, 16666m, 0m, 0.15m);
        AddBracket(semiMonthly, 16667m, 33332m, 937.50m, 0.20m);
        AddBracket(semiMonthly, 33333m, 83332m, 4270.70m, 0.25m);
        AddBracket(semiMonthly, 83333m, 333332m, 16770.70m, 0.30m);
        AddBracket(semiMonthly, 333333m, 9_999_999.99m, 91770.70m, 0.35m);

        var monthly = PayrollFrequency.MONTHLY.ToString();
        AddBracket(monthly, 0m, 20833m, 0m, 0m);
        AddBracket(monthly, 20833m, 33332m, 0m, 0.15m);
        AddBracket(monthly, 33333m, 66666m, 1875.00m, 0.20m);
        AddBracket(monthly, 66667m, 166666m, 8541.80m, 0.25m);
        AddBracket(monthly, 166667m, 666666m, 33541.80m, 0.30m);
        AddBracket(monthly, 666667m, 9_999_999.99m, 183541.80m, 0.35m);

        await _uow.Repository.AddRangeAsync(table, token);
    }

    /// <summary>
    /// Seeds the annual BIR income tax table (TRAIN Law second-phase rates, unchanged
    /// since Jan 1, 2023) — used for annualized/13th-month-adjacent computations.
    /// </summary>
    private async Task SetDefaultAnnualTaxTable(CancellationToken token)
    {
        var effectiveDate = new DateOnly(2026, 1, 1);
        var table = new List<AnnualTaxTable>
        {
            new AnnualTaxTable { Id = Guid.CreateVersion7(), EffectiveDate = effectiveDate, RangeFrom = 0m,             RangeTo = 250000m,         BaseTaxDue = 0m,          AddOnPercentage = 0m },
            new AnnualTaxTable { Id = Guid.CreateVersion7(), EffectiveDate = effectiveDate, RangeFrom = 250000.01m,     RangeTo = 400000m,         BaseTaxDue = 0m,          AddOnPercentage = 0.15m },
            new AnnualTaxTable { Id = Guid.CreateVersion7(), EffectiveDate = effectiveDate, RangeFrom = 400000.01m,     RangeTo = 800000m,         BaseTaxDue = 22500m,      AddOnPercentage = 0.20m },
            new AnnualTaxTable { Id = Guid.CreateVersion7(), EffectiveDate = effectiveDate, RangeFrom = 800000.01m,     RangeTo = 2000000m,        BaseTaxDue = 102500m,     AddOnPercentage = 0.25m },
            new AnnualTaxTable { Id = Guid.CreateVersion7(), EffectiveDate = effectiveDate, RangeFrom = 2000000.01m,    RangeTo = 8000000m,        BaseTaxDue = 402500m,     AddOnPercentage = 0.30m },
            new AnnualTaxTable { Id = Guid.CreateVersion7(), EffectiveDate = effectiveDate, RangeFrom = 8000000.01m,    RangeTo = 999_999_999.99m, BaseTaxDue = 2202500m,    AddOnPercentage = 0.35m },
        };
        await _uow.Repository.AddRangeAsync(table, token);
    }

    /// <summary>
    /// Seeds one starter PayrollGroup per PayrollFrequency, each with the CutoffDays
    /// CutoffPolicyResolver needs (it throws CutoffMismatchException if a group has none).
    /// Semi-Monthly gets two cutoffs (15th, end of month); Weekly gets four (roughly every
    /// 7 days, closing on end of month); Monthly and Daily each get a single end-of-month
    /// cutoff — Daily doesn't use it for cutoff timing (always "last cutoff"), but
    /// GetCurrentCutoff still requires at least one row to be configured.
    /// </summary>
    private async Task SetDefaultPayrollGroups(CancellationToken token)
    {
        var groups = new List<PayrollGroup>
        {
            new PayrollGroup
            {
                Id = Guid.CreateVersion7(),
                Code = "SM",
                Name = "Semi-Monthly",
                PayrollFrequency = PayrollFrequency.SEMI_MONTHLY,
                CutoffDays = new List<CutoffDay>
                {
                    new CutoffDay { Id = Guid.CreateVersion7(), Day = 15, Label = "1st Cutoff", IsEndOfMonth = false },
                    new CutoffDay { Id = Guid.CreateVersion7(), Day = 30, Label = "2nd Cutoff (End of Month)", IsEndOfMonth = true },
                },
            },
            new PayrollGroup
            {
                Id = Guid.CreateVersion7(),
                Code = "M",
                Name = "Monthly",
                PayrollFrequency = PayrollFrequency.MONTHLY,
                CutoffDays = new List<CutoffDay>
                {
                    new CutoffDay { Id = Guid.CreateVersion7(), Day = 30, Label = "Monthly Cutoff (End of Month)", IsEndOfMonth = true },
                },
            },
            new PayrollGroup
            {
                Id = Guid.CreateVersion7(),
                Code = "W",
                Name = "Weekly",
                PayrollFrequency = PayrollFrequency.WEEKLY,
                CutoffDays = new List<CutoffDay>
                {
                    new CutoffDay { Id = Guid.CreateVersion7(), Day = 7,  Label = "Week 1", IsEndOfMonth = false },
                    new CutoffDay { Id = Guid.CreateVersion7(), Day = 14, Label = "Week 2", IsEndOfMonth = false },
                    new CutoffDay { Id = Guid.CreateVersion7(), Day = 21, Label = "Week 3", IsEndOfMonth = false },
                    new CutoffDay { Id = Guid.CreateVersion7(), Day = 30, Label = "Week 4 (End of Month)", IsEndOfMonth = true },
                },
            },
            //new PayrollGroup
            //{
            //    Id = Guid.CreateVersion7(),
            //    Code = "D",
            //    Name = "Daily",
            //    PayrollFrequency = PayrollFrequency.DAILY,
            //    CutoffDays = new List<CutoffDay>
            //    {
            //        new CutoffDay { Id = Guid.CreateVersion7(), Day = 30, Label = "Month-End Settlement", IsEndOfMonth = true },
            //    },
            //},
        };
        await _uow.Repository.AddRangeAsync(groups, token);
    }

    /// <summary>
    /// Seeds a single starter TimeShift (Fixed, 8am-5pm) so a new tenant has a real shift to
    /// assign employees to immediately.
    /// </summary>
    private async Task SetDefaultTimeShifts(CancellationToken token)
    {
        var shifts = new List<TimeShift>
        {
            new TimeShift
            {
                Id = Guid.CreateVersion7(),
                ShiftName = "Day Shift",
                ShiftType = TimeShiftType.FIXED,
                StartTime = new TimeSpan(8, 0, 0),
                EndTime = new TimeSpan(17, 0, 0),
                WithAMBreak = BreakMode.NONE,
                WithLunchBreak = BreakMode.UNPAID_BREAK,
                LunchStartTime = new TimeSpan(12, 0, 0),
                LunchEndTime = new TimeSpan(13, 0, 0),
                WithPMBreak = BreakMode.NONE,
                GracePeriodMinutes = 0,
                BreakDurationMinutes = 60,
                WithOT = true,
                OTRequireTimeIn = false,
                OTStart = new TimeSpan(17, 0, 0),
                OverTimeThreshold = 60,
                MaxOvertimeHours = null, // no cap by default — see TimeShift.MaxOvertimeHours
                MinimumWorkMinutes = 0,
                MaxWorkingMinutes = 480,
            }
        };
        await _uow.Repository.AddRangeAsync(shifts, token);
    }

    /// <summary>
    /// Seeds the single per-tenant Fixed Salary Inclusion Defaults row (the company-wide
    /// fallback EmployeePayrollInclusionResolver uses when an employee has UseEmployeeOverride
    /// off) so every tenant starts with a real, editable row instead of relying on the GET
    /// endpoint's transient all-false fallback. All-false matches that same fallback, so this
    /// seed is a no-op for actual payroll behavior until an admin changes the settings.
    /// </summary>
    //private async Task SetDefaultPayrollInclusionDefaults(CancellationToken token)
    //{
    //    var defaults = new List<PayrollInclusionDefaults>
    //    {
    //        new PayrollInclusionDefaults
    //        {
    //            Id = Guid.CreateVersion7(),
    //            DefaultRestDayPaid = false,
    //            DefaultRegularHolidayIncluded = true,
    //            DefaultSpecialNonWorkingIncluded = false,
    //        },
    //    };
    //    await _uow.Repository.AddRangeAsync(defaults, token);
    //}

    private async Task SetDefaultDeductionTypes(CancellationToken token)
    {
        var types = new List<DeductionType>
        {
            new DeductionType { Id = Guid.CreateVersion7(), Code = "GOVT",    Name = "Government Contributions"  , Status="ACTIVE" },
            new DeductionType { Id = Guid.CreateVersion7(), Code = "COLOAN",  Name = "Company Loan" , Status="ACTIVE" },
            new DeductionType { Id = Guid.CreateVersion7(), Code = "SSSLOAN", Name = "SSS Loan"  , Status="ACTIVE"},
            new DeductionType { Id = Guid.CreateVersion7(), Code = "HDMFLOAN", Name = "Pag-IBIG (HDMF) Loan" , Status="ACTIVE" },
            new DeductionType { Id = Guid.CreateVersion7(), Code = "CASHADV", Name = "Cash Advance" , Status="ACTIVE" },
            new DeductionType { Id = Guid.CreateVersion7(), Code = "SALLOAN", Name = "Salary Loan"  , Status="ACTIVE"},
            new DeductionType { Id = Guid.CreateVersion7(), Code = "CALLOAN", Name = "Calamity Loan"  , Status="ACTIVE"},
            new DeductionType { Id = Guid.CreateVersion7(), Code = "MEDDENT", Name = "Medical / Dental" , Status="ACTIVE" },
            new DeductionType { Id = Guid.CreateVersion7(), Code = "OTHER",   Name = "Other Deductions"  , Status="ACTIVE"},
        };
        await _uow.Repository.AddRangeAsync(types, token);
    }

}
