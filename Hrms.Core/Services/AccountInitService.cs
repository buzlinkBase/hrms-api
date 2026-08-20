
using DTR.Core;
using Hrms.Domain.Entities;
namespace Hrms.Core.Services;

public class AccountInitService : BaseService<Company>
{
    private readonly GeneralSettingService _settingService;
    public AccountInitService(IUnitOfWorkService uow, GeneralSettingService settingService) : base(uow)
    {
        _settingService = settingService;
    }
    public async Task Create(CancellationToken token)
    {
        //await CreateOrUpdateAsync(company);
        await SetDefaultLeaves(token);
        await SetDefaultRates(token);
        await SetDefaultIncomeTypes(token);
        await SetDefaultDeductionTypes(token);
        await PayrollSettings(token);
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
            new RateTable { Id = Guid.CreateVersion7(), Type = RateType.HOLIDAY_OT,         ShortDescription = "HOLIDAY_OT",         Description = "Holiday / Rest Day OT Premium", Rate = 1.30m },
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
    private async Task SetDefaultDeductionTypes(CancellationToken token)
    {
        var types = new List<DeductionType>
        {
            new DeductionType { Id = Guid.CreateVersion7(), Description = "Government Contributions" },
            new DeductionType { Id = Guid.CreateVersion7(), Description = "Company Loan" },
            new DeductionType { Id = Guid.CreateVersion7(), Description = "SSS Loan" },
            new DeductionType { Id = Guid.CreateVersion7(), Description = "Pag-IBIG (HDMF) Loan" },
            new DeductionType { Id = Guid.CreateVersion7(), Description = "Cash Advance" },
            new DeductionType { Id = Guid.CreateVersion7(), Description = "Salary Loan" },
            new DeductionType { Id = Guid.CreateVersion7(), Description = "Calamity Loan" },
            new DeductionType { Id = Guid.CreateVersion7(), Description = "Medical / Dental" },
            new DeductionType { Id = Guid.CreateVersion7(), Description = "Other Deductions" },
        };
        await _uow.Repository.AddRangeAsync(types, token);
    }

}
