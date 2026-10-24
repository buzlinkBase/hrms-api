using DTR.Core;
using Hrms.Domain.ValueObjects;

namespace Hrms.Core.Services;

/// <summary>The My Portal pages a company can switch off. Dashboard and Profile are always on.</summary>
public enum PortalPage
{
    ThirteenthMonth,
    CashBond,
    UniformAllowance,
    Announcements,
    Coworkers,
    Dtr,
    IncompletePunches,
    RawAttendance,
    ShiftSchedule,
    Payslips,
    LeaveCredits,
    Deductions,
    LeaveApplications,
    Overtime,
    OfficialBusiness,
    PassSlip,
    LoanApplications,
}

/// <summary>Every page is on unless the company saved its switch off.</summary>
public record PortalVisibilityModel
{
    public bool ShowThirteenthMonth { get; init; } = true;
    public bool ShowCashBond { get; init; } = true;
    public bool ShowUniformAllowance { get; init; } = true;
    public bool ShowAnnouncements { get; init; } = true;
    public bool ShowCoworkers { get; init; } = true;
    public bool ShowDtr { get; init; } = true;
    public bool ShowIncompletePunches { get; init; } = true;
    public bool ShowRawAttendance { get; init; } = true;
    public bool ShowShiftSchedule { get; init; } = true;
    public bool ShowPayslips { get; init; } = true;
    public bool ShowLeaveCredits { get; init; } = true;
    public bool ShowDeductions { get; init; } = true;
    public bool ShowLeaveApplications { get; init; } = true;
    public bool ShowOvertime { get; init; } = true;
    public bool ShowOfficialBusiness { get; init; } = true;
    public bool ShowPassSlip { get; init; } = true;
    public bool ShowLoanApplications { get; init; } = true;

    public bool IsVisible(PortalPage page) => page switch
    {
        PortalPage.ThirteenthMonth => ShowThirteenthMonth,
        PortalPage.CashBond => ShowCashBond,
        PortalPage.UniformAllowance => ShowUniformAllowance,
        PortalPage.Announcements => ShowAnnouncements,
        PortalPage.Coworkers => ShowCoworkers,
        PortalPage.Dtr => ShowDtr,
        PortalPage.IncompletePunches => ShowIncompletePunches,
        PortalPage.RawAttendance => ShowRawAttendance,
        PortalPage.ShiftSchedule => ShowShiftSchedule,
        PortalPage.Payslips => ShowPayslips,
        PortalPage.LeaveCredits => ShowLeaveCredits,
        PortalPage.Deductions => ShowDeductions,
        PortalPage.LeaveApplications => ShowLeaveApplications,
        PortalPage.Overtime => ShowOvertime,
        PortalPage.OfficialBusiness => ShowOfficialBusiness,
        PortalPage.PassSlip => ShowPassSlip,
        PortalPage.LoanApplications => ShowLoanApplications,
        _ => true,
    };
}

/// <summary>
/// Which "My Portal" pages the company switched on for the Employee Portal (Company Settings >
/// Payroll Settings). Every page defaults to on when the company never saved the switch, so
/// existing companies keep what they already had.
/// </summary>
public class PortalVisibilityService
{
    private readonly GeneralSettingService _settings;

    public PortalVisibilityService(GeneralSettingService settings)
    {
        _settings = settings;
    }

    public async Task<PortalVisibilityModel> GetAsync()
    {
        var s = await _settings.GetSettingsAsync(PayrollSettingsIdentity.IdentityType);

        bool Read(string key) =>
            !s.TryGetValue(key, out var setting) || GeneralSettingsUtil.ParseBool(setting.Value, true);

        return new PortalVisibilityModel
        {
            ShowThirteenthMonth = Read(PayrollSettingsIdentity.KeyPortalShowThirteenthMonth),
            ShowCashBond = Read(PayrollSettingsIdentity.KeyPortalShowCashBond),
            ShowUniformAllowance = Read(PayrollSettingsIdentity.KeyPortalShowUniformAllowance),
            ShowAnnouncements = Read(PayrollSettingsIdentity.KeyPortalShowAnnouncements),
            ShowCoworkers = Read(PayrollSettingsIdentity.KeyPortalShowCoworkers),
            ShowDtr = Read(PayrollSettingsIdentity.KeyPortalShowDtr),
            ShowIncompletePunches = Read(PayrollSettingsIdentity.KeyPortalShowIncompletePunches),
            ShowRawAttendance = Read(PayrollSettingsIdentity.KeyPortalShowRawAttendance),
            ShowShiftSchedule = Read(PayrollSettingsIdentity.KeyPortalShowShiftSchedule),
            ShowPayslips = Read(PayrollSettingsIdentity.KeyPortalShowPayslips),
            ShowLeaveCredits = Read(PayrollSettingsIdentity.KeyPortalShowLeaveCredits),
            ShowDeductions = Read(PayrollSettingsIdentity.KeyPortalShowDeductions),
            ShowLeaveApplications = Read(PayrollSettingsIdentity.KeyPortalShowLeaveApplications),
            ShowOvertime = Read(PayrollSettingsIdentity.KeyPortalShowOvertime),
            ShowOfficialBusiness = Read(PayrollSettingsIdentity.KeyPortalShowOfficialBusiness),
            ShowPassSlip = Read(PayrollSettingsIdentity.KeyPortalShowPassSlip),
            ShowLoanApplications = Read(PayrollSettingsIdentity.KeyPortalShowLoanApplications),
        };
    }
}
