using DTR.Core;

namespace Hrms.Core.Services;

// Which portal Coworkers widgets the company has switched on. Stored as Company GeneralSetting rows
// (edited with the rest of Company Policy); a missing row means off, so nothing is shared until HR
// opts in.
public record CoworkerWidgetSettings(bool ShowNewHires, bool ShowAnniversaries, bool ShowWhosOut, bool ShowDirectory)
{
    public static readonly CoworkerWidgetSettings AllOff = new(false, false, false, false);

    public static CoworkerWidgetSettings From(IReadOnlyDictionary<string, GeneralSettingModel> company) => new(
        Read(company, SettingKey.CoworkerShowNewHires),
        Read(company, SettingKey.CoworkerShowAnniversaries),
        Read(company, SettingKey.CoworkerShowWhosOut),
        Read(company, SettingKey.CoworkerShowDirectory));

    private static bool Read(IReadOnlyDictionary<string, GeneralSettingModel> company, SettingKey key) =>
        company.TryGetValue(key.ToString(), out var setting) && GeneralSettingsUtil.ParseBool(setting.Value, false);
}
