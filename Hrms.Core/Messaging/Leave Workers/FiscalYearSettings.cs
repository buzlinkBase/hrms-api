using Hrms.Domain.Entities;

namespace Hrms.Core.Messaging.LeaveWorkers;

/// <summary>
/// Where a company's fiscal-year start month lives (a GeneralSetting row) and how it's read.
/// Missing or invalid means 1 (calendar year).
/// </summary>
public static class FiscalYearSettings
{
    public const string IdentityType = "PayrollSettings";
    public const string Key = "FiscalYearStartMonth";

    public static bool IsStartMonthSetting(GeneralSetting setting) =>
        setting.IdentityType == IdentityType && setting.Description == Key;

    public static int Parse(string? value) =>
        int.TryParse(value, out var month) && month is >= 1 and <= 12 ? month : 1;

    /// <summary>The current company's start month (the query is company-filtered as usual).</summary>
    public static async Task<int> GetStartMonthAsync(IQueryable<GeneralSetting> settings, CancellationToken token) =>
        Parse(await settings
            .Where(x => x.IdentityType == IdentityType && x.Description == Key)
            .Select(x => x.Value)
            .FirstOrDefaultAsync(token));
}
