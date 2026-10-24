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
}