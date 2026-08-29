using Asp.Versioning;
using DTR.Core;
using Hrms.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Controllers;


[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
[ApiController]
[ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 400)]
[ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 401)]
[ProducesResponseType(typeof(ResponseModel<ProblemDetails>), 500)]
public class GeneralSettingsController : ControllerBase
{
    private readonly GeneralSettingService _settingService;
    private readonly CompanyPolicyService _policyService;

    public GeneralSettingsController(GeneralSettingService settingService, CompanyPolicyService policyService)
    {
        _settingService = settingService;
        _policyService = policyService;
    }

    [HttpGet("company")]
    [ProducesResponseType(typeof(ResponseModel<CompanyPolicyResponse>), 200)]
    public async Task<IActionResult> GetCompanyPolicy(CancellationToken token)
    {
        var settings = await _settingService.GetSettingsAsync("Company");
        var policy = _policyService.Transform(settings);
        var response = new CompanyPolicyResponse
        {
            OtInclusionPolicy = policy.OTInclusionPolicy.ToString(),
            OtEligibility = policy.OTEligibility.ToString(),
            IsHalfDayLateOn = policy.IsHalfDayLateOn,
            IsWholeDayLateOn = policy.IsWholeDayLateOn,
            HalfDayLateThresholdMinutes = policy.HalfDayLateThresholdMinutes,
            WholeDayLateThresholdMinutes = policy.WholeDayLateThresholdMinutes,
            NightDiffThreshold = policy.NightDiffThreshold,
            AttFillLimit = policy.AttFillLimit.ToString(),
            HolidayTimeBasis = policy.HolidayTimeBasis.ToString(),
            IsHolPlusReg = policy.IsHolPlusReg,
            TimeInAllowance = policy.TimeInAllowance,
            DoublePunchGap = policy.DoublePunchGap,
            CheckAfterHoliday = policy.CheckAfterHoliday,
            WaivePriorDayRequirement = policy.WaivePriorDayRequirement,
            CrossMonthStatutoryCreditPolicy = policy.CrossMonthStatutoryCreditPolicy.ToString(),
            WTaxCrossMonthCreditPolicy = policy.WTaxCrossMonthCreditPolicy.ToString(),
            TreatNdotAsNdOnly = policy.TreatNdotAsNdOnly,
        };
        return Ok(response);
    }

    [HttpPut("company")]
    [ProducesResponseType(typeof(ResponseModel<object>), 200)]
    public async Task<IActionResult> UpdateCompanyPolicy(
        [FromBody] UpdateCompanyPolicyRequest request,
        CancellationToken token)
    {
        var settings = new List<GeneralSetting>
        {
            new() { IdentityType = "Company", Description = SettingKey.OTInclusion.ToString(), Value = request.OtInclusionPolicy },
            new() { IdentityType = "Company", Description = SettingKey.OTEligibility.ToString(), Value = request.OtEligibility },
            new() { IdentityType = "Company", Description = SettingKey.AttFillLimit.ToString(), Value = request.AttFillLimit },
            new() { IdentityType = "Company", Description = SettingKey.HolidayTimeBasis.ToString(), Value = request.HolidayTimeBasis },
            new() { IdentityType = "Company", Description = SettingKey.IsHalfDayLateOn.ToString(), Value = request.IsHalfDayLateOn.ToString().ToLower() },
            new() { IdentityType = "Company", Description = SettingKey.IsWholeDayLateOn.ToString(), Value = request.IsWholeDayLateOn.ToString().ToLower() },
            new() { IdentityType = "Company", Description = SettingKey.HalfDayLateThresholdMinutes.ToString(), Value = request.HalfDayLateThresholdMinutes.ToString() },
            new() { IdentityType = "Company", Description = SettingKey.WholeDayLateThresholdMinutes.ToString(), Value = request.WholeDayLateThresholdMinutes.ToString() },
            new() { IdentityType = "Company", Description = SettingKey.NightDiffThreshold.ToString(), Value = request.NightDiffThreshold.ToString() },
            new() { IdentityType = "Company", Description = SettingKey.IsHolPlusReg.ToString(), Value = request.IsHolPlusReg.ToString().ToLower() },
            new() { IdentityType = "Company", Description = SettingKey.TimeInAllowance.ToString(), Value = request.TimeInAllowance.ToString() },
            new() { IdentityType = "Company", Description = SettingKey.DoublePunchGap.ToString(), Value = request.DoublePunchGap.ToString() },
            new() { IdentityType = "Company", Description = SettingKey.CheckAfterHoliday.ToString(), Value = request.CheckAfterHoliday.ToString().ToLower() },
            new() { IdentityType = "Company", Description = SettingKey.WaivePriorDayRequirement.ToString(), Value = request.WaivePriorDayRequirement.ToString().ToLower() },
            new() { IdentityType = "Company", Description = SettingKey.CrossMonthStatutoryCreditPolicy.ToString(), Value = request.CrossMonthStatutoryCreditPolicy },
            new() { IdentityType = "Company", Description = SettingKey.WTaxCrossMonthCreditPolicy.ToString(), Value = request.WTaxCrossMonthCreditPolicy },
            new() { IdentityType = "Company", Description = SettingKey.TreatNdotAsNdOnly.ToString(), Value = request.TreatNdotAsNdOnly.ToString().ToLower() },
        };

        await _settingService.ReplaceByIdentityTypeAsync("Company", settings, null, token);
        return Ok("success");
    }

    [HttpGet("client/{clientId:guid}")]
    [ProducesResponseType(typeof(ResponseModel<ClientPolicyDto>), 200)]
    public async Task<IActionResult> GetClientPolicy(Guid clientId, CancellationToken token)
    {
        var clientSettings = await _settingService.GetSettingsAsync("Client", new HashSet<string> { clientId.ToString() });
        var dict = clientSettings.TryGetValue(new SettingGroupKey(clientId), out var d)
            ? d
            : new Dictionary<string, GeneralSettingModel>();

        return Ok(new ClientPolicyDto
        {
            OtEligibility = dict.TryGetValue(SettingKey.OTEligibility.ToString(), out var e) ? e.Value : null,
            OtInclusionPolicy = dict.TryGetValue(SettingKey.OTInclusion.ToString(), out var i) ? i.Value : null,
            TreatNdotAsNdOnly = dict.TryGetValue(SettingKey.TreatNdotAsNdOnly.ToString(), out var n)
                ? GeneralSettingsUtil.ParseBool(n.Value, false)
                : null,
        });
    }

    [HttpPut("client/{clientId:guid}")]
    [ProducesResponseType(typeof(ResponseModel<object>), 200)]
    public async Task<IActionResult> UpdateClientPolicy(Guid clientId, [FromBody] ClientPolicyRequest request, CancellationToken token)
    {
        var settings = new List<GeneralSetting>();
        if (!string.IsNullOrWhiteSpace(request.OtEligibility))
            settings.Add(new() { IdentityType = "Client", IdentityTypeId = clientId.ToString(), Description = SettingKey.OTEligibility.ToString(), Value = request.OtEligibility });
        if (!string.IsNullOrWhiteSpace(request.OtInclusionPolicy))
            settings.Add(new() { IdentityType = "Client", IdentityTypeId = clientId.ToString(), Description = SettingKey.OTInclusion.ToString(), Value = request.OtInclusionPolicy });
        if (request.TreatNdotAsNdOnly.HasValue)
            settings.Add(new() { IdentityType = "Client", IdentityTypeId = clientId.ToString(), Description = SettingKey.TreatNdotAsNdOnly.ToString(), Value = request.TreatNdotAsNdOnly.Value.ToString().ToLower() });
        await _settingService.ReplaceByIdentityTypeAsync("Client", settings, clientId.ToString(), token);
        return Ok("success");
    }
}

public class UpdateCompanyPolicyRequest
{
    public string OtInclusionPolicy { get; set; } = "";
    public string OtEligibility { get; set; } = "";
    public bool IsHalfDayLateOn { get; set; }
    public bool IsWholeDayLateOn { get; set; }
    public double HalfDayLateThresholdMinutes { get; set; }
    public double WholeDayLateThresholdMinutes { get; set; }
    public double NightDiffThreshold { get; set; }
    public string AttFillLimit { get; set; } = "";
    public string HolidayTimeBasis { get; set; } = "";
    public bool IsHolPlusReg { get; set; }
    public double TimeInAllowance { get; set; }
    public double DoublePunchGap { get; set; }
    public bool CheckAfterHoliday { get; set; }
    public bool WaivePriorDayRequirement { get; set; }
    public string CrossMonthStatutoryCreditPolicy { get; set; } = "";
    public string WTaxCrossMonthCreditPolicy { get; set; } = "";
    public bool TreatNdotAsNdOnly { get; set; }
}
public class CompanyPolicyResponse
{
    public required string OtInclusionPolicy { get; set; } = "";
    public required string OtEligibility { get; set; } = "";
    public required bool IsHalfDayLateOn { get; set; }
    public required bool IsWholeDayLateOn { get; set; }
    public required double HalfDayLateThresholdMinutes { get; set; }
    public required double WholeDayLateThresholdMinutes { get; set; }
    public required double NightDiffThreshold { get; set; }
    public required string AttFillLimit { get; set; } = "";
    public required string HolidayTimeBasis { get; set; } = "";
    public required bool IsHolPlusReg { get; set; }
    public required double TimeInAllowance { get; set; }
    public required double DoublePunchGap { get; set; }
    public required bool CheckAfterHoliday { get; set; }
    public required bool WaivePriorDayRequirement { get; set; }
    public required string CrossMonthStatutoryCreditPolicy { get; set; } = "";
    public required string WTaxCrossMonthCreditPolicy { get; set; } = "";
    public required bool TreatNdotAsNdOnly { get; set; }
}
public class ClientPolicyDto
{
    public string? OtEligibility { get; set; }
    public string? OtInclusionPolicy { get; set; }
    public bool? TreatNdotAsNdOnly { get; set; }
}
public class ClientPolicyRequest
{
    public string? OtEligibility { get; set; }
    public string? OtInclusionPolicy { get; set; }
    public bool? TreatNdotAsNdOnly { get; set; }
}