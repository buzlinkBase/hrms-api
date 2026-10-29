
namespace Hrms.Domain.ValueObjects;

public static class PayrollSettingsIdentity
{
    public const string IdentityType = "PayrollSettings";
    public const string KeyFiscalYearStartMonth = "FiscalYearStartMonth";
    public const string KeyThirteenthMonthExemptionCeiling = "ThirteenthMonthExemptionCeiling";
    // Multiplier against an employee's CurrentAverageMonthlyNetPay — a Year-End Tax
    // Annualization collection larger than (average monthly net pay * this) is flagged with a
    // warning in Preview (informational only, never blocks Generate). Default 1.0 — see
    // TaxAnnualizationService.ComputeAsync.
    public const string KeyLargeTaxCollectionWarningMultiplier = "LargeTaxCollectionWarningMultiplier";
    // Employee Portal ("My Pay & Benefits") pages the company can switch off. Default on when
    // unset — see PortalVisibilityService.
    public const string KeyPortalShowThirteenthMonth = "PortalShowThirteenthMonth";
    public const string KeyPortalShowCashBond = "PortalShowCashBond";
    public const string KeyPortalShowUniformAllowance = "PortalShowUniformAllowance";
    public const string KeyPortalShowAnnouncements = "PortalShowAnnouncements";
    public const string KeyPortalShowCoworkers = "PortalShowCoworkers";
    public const string KeyPortalShowDtr = "PortalShowDtr";
    public const string KeyPortalShowIncompletePunches = "PortalShowIncompletePunches";
    public const string KeyPortalShowRawAttendance = "PortalShowRawAttendance";
    public const string KeyPortalShowShiftSchedule = "PortalShowShiftSchedule";
    public const string KeyPortalShowPayslips = "PortalShowPayslips";
    public const string KeyPortalShowLeaveCredits = "PortalShowLeaveCredits";
    public const string KeyPortalShowDeductions = "PortalShowDeductions";
    public const string KeyPortalShowLeaveApplications = "PortalShowLeaveApplications";
    public const string KeyPortalShowOvertime = "PortalShowOvertime";
    public const string KeyPortalShowOfficialBusiness = "PortalShowOfficialBusiness";
    public const string KeyPortalShowPassSlip = "PortalShowPassSlip";
    public const string KeyPortalShowLoanApplications = "PortalShowLoanApplications";
    // Punch In/Out from My Portal. Unlike the pages above this one is OFF until the company turns
    // it on, and consecutive punches must be at least PortalPunchCooldownMinutes apart (default 5).
    public const string KeyPortalShowPunch = "PortalShowPunch";
    public const string KeyPortalPunchCooldownMinutes = "PortalPunchCooldownMinutes";
    public const int DefaultPortalPunchCooldownMinutes = 5;
}
