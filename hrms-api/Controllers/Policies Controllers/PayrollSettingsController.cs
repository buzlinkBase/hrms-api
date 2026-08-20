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
public class PayrollSettingsController : ControllerBase
{
    private const string IdentityType = PayrollSettingsIdentity.IdentityType;
    private const string KeyFiscalYearStartMonth = PayrollSettingsIdentity.KeyFiscalYearStartMonth;
    private const string KeyThirteenthMonthExemptionCeiling = PayrollSettingsIdentity.KeyThirteenthMonthExemptionCeiling;

    private readonly GeneralSettingService _settingService;
    public PayrollSettingsController(GeneralSettingService settingService)
    {
        _settingService = settingService;
    }

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
        };

        await _settingService.ReplaceByIdentityTypeAsync(IdentityType, incoming, null, token);

        return Ok(request);
    }
}

public class PayrollSettingsDto
{
    public int FiscalYearStartMonth { get; set; }
    public double ThirteenthMonthExemptionCeiling { get; set; }
} 