using DTR.Core;
using Hrms.Domain.ValueObjects;

namespace Hrms.Core.Services;

public record PortalVisibilityModel(bool ShowThirteenthMonth, bool ShowCashBond, bool ShowUniformAllowance);

/// <summary>
/// Which optional "My Pay &amp; Benefits" pages the company switched on for the Employee Portal.
/// Every page defaults to on when the company never saved the switch, so existing companies keep
/// what they already had.
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

        return new PortalVisibilityModel(
            Read(PayrollSettingsIdentity.KeyPortalShowThirteenthMonth),
            Read(PayrollSettingsIdentity.KeyPortalShowCashBond),
            Read(PayrollSettingsIdentity.KeyPortalShowUniformAllowance));
    }
}
