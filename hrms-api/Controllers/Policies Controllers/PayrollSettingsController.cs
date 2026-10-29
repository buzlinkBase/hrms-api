using Onepunch.Common.Lib.Subscriptions;
using Hrms.Api.Filters;
using Asp.Versioning;
using Hrms.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
namespace Hrms.Api.Controllers;

[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
[ApiController]
[ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 400)]
[ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 401)]
[ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 500)]
[RequireFeature(EntitlementKeys.PayrollCore, WritesOnly = true)]
public class PayrollSettingsController : ControllerBase
{
    private const string IdentityType = PayrollSettingsIdentity.IdentityType;
    private const string KeyFiscalYearStartMonth = PayrollSettingsIdentity.KeyFiscalYearStartMonth;
    private const string KeyThirteenthMonthExemptionCeiling = PayrollSettingsIdentity.KeyThirteenthMonthExemptionCeiling;
    private const string KeyLargeTaxCollectionWarningMultiplier = PayrollSettingsIdentity.KeyLargeTaxCollectionWarningMultiplier;

    private readonly GeneralSettingService _settingService;
    public PayrollSettingsController(GeneralSettingService settingService)
    {
        _settingService = settingService;
    }

    // Unsaved portal switches read as on, so existing companies keep their current portal.
    private static bool ReadFlag(Dictionary<string, GeneralSettingModel> settings, string key) =>
        !settings.TryGetValue(key, out var setting) || GeneralSettingsUtil.ParseBool(setting.Value, true);

    [HttpGet]
    [ProducesResponseType(typeof(ResponseModel<PayrollSettingsDto>), 200)]
    public async Task<IActionResult> Get(CancellationToken token)
    {
        var settings = await _settingService.GetSettingsAsync(IdentityType);

        return Ok(new PayrollSettingsDto
        {
            FiscalYearStartMonth = settings.TryGetValue(KeyFiscalYearStartMonth, out var fy) && fy.Value!=null
                ? GeneralSettingsUtil.ParseInt(fy.Value, 1)
                : 1,
            ThirteenthMonthExemptionCeiling = settings.TryGetValue(KeyThirteenthMonthExemptionCeiling, out var ceiling) && ceiling.Value != null
                ? GeneralSettingsUtil.ParseDouble(ceiling.Value, 0)
                : 0,
            LargeTaxCollectionWarningMultiplier = settings.TryGetValue(KeyLargeTaxCollectionWarningMultiplier, out var multiplier) && multiplier.Value != null
                ? GeneralSettingsUtil.ParseDouble(multiplier.Value, 1.0)
                : 1.0,
            PortalShowThirteenthMonth = ReadFlag(settings, PayrollSettingsIdentity.KeyPortalShowThirteenthMonth),
            PortalShowCashBond = ReadFlag(settings, PayrollSettingsIdentity.KeyPortalShowCashBond),
            PortalShowUniformAllowance = ReadFlag(settings, PayrollSettingsIdentity.KeyPortalShowUniformAllowance),
            PortalShowAnnouncements = ReadFlag(settings, PayrollSettingsIdentity.KeyPortalShowAnnouncements),
            PortalShowCoworkers = ReadFlag(settings, PayrollSettingsIdentity.KeyPortalShowCoworkers),
            PortalShowDtr = ReadFlag(settings, PayrollSettingsIdentity.KeyPortalShowDtr),
            PortalShowIncompletePunches = ReadFlag(settings, PayrollSettingsIdentity.KeyPortalShowIncompletePunches),
            PortalShowRawAttendance = ReadFlag(settings, PayrollSettingsIdentity.KeyPortalShowRawAttendance),
            PortalShowShiftSchedule = ReadFlag(settings, PayrollSettingsIdentity.KeyPortalShowShiftSchedule),
            PortalShowPayslips = ReadFlag(settings, PayrollSettingsIdentity.KeyPortalShowPayslips),
            PortalShowLeaveCredits = ReadFlag(settings, PayrollSettingsIdentity.KeyPortalShowLeaveCredits),
            PortalShowDeductions = ReadFlag(settings, PayrollSettingsIdentity.KeyPortalShowDeductions),
            PortalShowLeaveApplications = ReadFlag(settings, PayrollSettingsIdentity.KeyPortalShowLeaveApplications),
            PortalShowOvertime = ReadFlag(settings, PayrollSettingsIdentity.KeyPortalShowOvertime),
            PortalShowOfficialBusiness = ReadFlag(settings, PayrollSettingsIdentity.KeyPortalShowOfficialBusiness),
            PortalShowPassSlip = ReadFlag(settings, PayrollSettingsIdentity.KeyPortalShowPassSlip),
            PortalShowLoanApplications = ReadFlag(settings, PayrollSettingsIdentity.KeyPortalShowLoanApplications),
            PortalShowPunch = settings.TryGetValue(PayrollSettingsIdentity.KeyPortalShowPunch, out var punch)
                && GeneralSettingsUtil.ParseBool(punch.Value, false),
            PortalPunchCooldownMinutes = settings.TryGetValue(PayrollSettingsIdentity.KeyPortalPunchCooldownMinutes, out var cooldown)
                ? GeneralSettingsUtil.ParseInt(cooldown.Value, PayrollSettingsIdentity.DefaultPortalPunchCooldownMinutes)
                : PayrollSettingsIdentity.DefaultPortalPunchCooldownMinutes,
            MobilePunchModesEnabled = settings.TryGetValue(PayrollSettingsIdentity.KeyMobilePunchModesEnabled, out var mobileModes)
                && GeneralSettingsUtil.ParseBool(mobileModes.Value, false),
        });
    }

    [HttpPut]
    [ProducesResponseType(typeof(ResponseModel<PayrollSettingsDto>), 200)]
    public async Task<IActionResult> Put([FromBody] PayrollSettingsDto request, CancellationToken token)
    {
        var incoming = new List<GeneralSetting>
        {
            new() { IdentityType = IdentityType, Description = KeyFiscalYearStartMonth, Value = request.FiscalYearStartMonth.ToString() },
            new() { IdentityType = IdentityType, Description = KeyThirteenthMonthExemptionCeiling, Value = request.ThirteenthMonthExemptionCeiling.ToString() },
            new() { IdentityType = IdentityType, Description = KeyLargeTaxCollectionWarningMultiplier, Value = request.LargeTaxCollectionWarningMultiplier.ToString() },
            new() { IdentityType = IdentityType, Description = PayrollSettingsIdentity.KeyPortalShowThirteenthMonth, Value = request.PortalShowThirteenthMonth.ToString() },
            new() { IdentityType = IdentityType, Description = PayrollSettingsIdentity.KeyPortalShowCashBond, Value = request.PortalShowCashBond.ToString() },
            new() { IdentityType = IdentityType, Description = PayrollSettingsIdentity.KeyPortalShowUniformAllowance, Value = request.PortalShowUniformAllowance.ToString() },
            new() { IdentityType = IdentityType, Description = PayrollSettingsIdentity.KeyPortalShowAnnouncements, Value = request.PortalShowAnnouncements.ToString() },
            new() { IdentityType = IdentityType, Description = PayrollSettingsIdentity.KeyPortalShowCoworkers, Value = request.PortalShowCoworkers.ToString() },
            new() { IdentityType = IdentityType, Description = PayrollSettingsIdentity.KeyPortalShowDtr, Value = request.PortalShowDtr.ToString() },
            new() { IdentityType = IdentityType, Description = PayrollSettingsIdentity.KeyPortalShowIncompletePunches, Value = request.PortalShowIncompletePunches.ToString() },
            new() { IdentityType = IdentityType, Description = PayrollSettingsIdentity.KeyPortalShowRawAttendance, Value = request.PortalShowRawAttendance.ToString() },
            new() { IdentityType = IdentityType, Description = PayrollSettingsIdentity.KeyPortalShowShiftSchedule, Value = request.PortalShowShiftSchedule.ToString() },
            new() { IdentityType = IdentityType, Description = PayrollSettingsIdentity.KeyPortalShowPayslips, Value = request.PortalShowPayslips.ToString() },
            new() { IdentityType = IdentityType, Description = PayrollSettingsIdentity.KeyPortalShowLeaveCredits, Value = request.PortalShowLeaveCredits.ToString() },
            new() { IdentityType = IdentityType, Description = PayrollSettingsIdentity.KeyPortalShowDeductions, Value = request.PortalShowDeductions.ToString() },
            new() { IdentityType = IdentityType, Description = PayrollSettingsIdentity.KeyPortalShowLeaveApplications, Value = request.PortalShowLeaveApplications.ToString() },
            new() { IdentityType = IdentityType, Description = PayrollSettingsIdentity.KeyPortalShowOvertime, Value = request.PortalShowOvertime.ToString() },
            new() { IdentityType = IdentityType, Description = PayrollSettingsIdentity.KeyPortalShowOfficialBusiness, Value = request.PortalShowOfficialBusiness.ToString() },
            new() { IdentityType = IdentityType, Description = PayrollSettingsIdentity.KeyPortalShowPassSlip, Value = request.PortalShowPassSlip.ToString() },
            new() { IdentityType = IdentityType, Description = PayrollSettingsIdentity.KeyPortalShowLoanApplications, Value = request.PortalShowLoanApplications.ToString() },
            new() { IdentityType = IdentityType, Description = PayrollSettingsIdentity.KeyPortalShowPunch, Value = request.PortalShowPunch.ToString() },
            new() { IdentityType = IdentityType, Description = PayrollSettingsIdentity.KeyPortalPunchCooldownMinutes, Value = Math.Clamp(request.PortalPunchCooldownMinutes, 1, 120).ToString() },
            new() { IdentityType = IdentityType, Description = PayrollSettingsIdentity.KeyMobilePunchModesEnabled, Value = request.MobilePunchModesEnabled.ToString() },
        };

        await _settingService.ReplaceByIdentityTypeAsync(IdentityType, incoming, null, token);

        return Ok(request);
    }
}

public class PayrollSettingsDto
{
    public int FiscalYearStartMonth { get; set; }
    public double ThirteenthMonthExemptionCeiling { get; set; }
    public double LargeTaxCollectionWarningMultiplier { get; set; }
    public bool PortalShowThirteenthMonth { get; set; } = true;
    public bool PortalShowCashBond { get; set; } = true;
    public bool PortalShowUniformAllowance { get; set; } = true;
    public bool PortalShowAnnouncements { get; set; } = true;
    public bool PortalShowCoworkers { get; set; } = true;
    public bool PortalShowDtr { get; set; } = true;
    public bool PortalShowIncompletePunches { get; set; } = true;
    public bool PortalShowRawAttendance { get; set; } = true;
    public bool PortalShowShiftSchedule { get; set; } = true;
    public bool PortalShowPayslips { get; set; } = true;
    public bool PortalShowLeaveCredits { get; set; } = true;
    public bool PortalShowDeductions { get; set; } = true;
    public bool PortalShowLeaveApplications { get; set; } = true;
    public bool PortalShowOvertime { get; set; } = true;
    public bool PortalShowOfficialBusiness { get; set; } = true;
    public bool PortalShowPassSlip { get; set; } = true;
    public bool PortalShowLoanApplications { get; set; } = true;
    public bool PortalShowPunch { get; set; }
    public int PortalPunchCooldownMinutes { get; set; } = PayrollSettingsIdentity.DefaultPortalPunchCooldownMinutes;
    public bool MobilePunchModesEnabled { get; set; }
}