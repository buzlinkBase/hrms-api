using System.Globalization;

namespace Hrms.adms.Models.DTO;

public interface ISystemClockService
{
    DateTimeOffset Now();
    string FormatForDevice();
    string NormalizeForDevice(object? value);
    string TimezoneName();
    string DeviceTimezoneOption();
}
public class SystemClockService : ISystemClockService
{
    private readonly IConfiguration _configuration;
    private readonly TimeZoneInfo _resolvedTimeZone;

    public SystemClockService(IConfiguration configuration)
    {
        _configuration = configuration;
        _resolvedTimeZone = ResolveTimeZone();
    }

    public DateTimeOffset Now()
    {
        // Capture accurate UTC instantly, then project it safely to our target offset
        return TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, _resolvedTimeZone);
    }

    public string FormatForDevice()
    {
        return Now().ToString("yyyy-MM-dd HH:mm:ss");
    }

    public string NormalizeForDevice(object? value)
    {
        string? valStr = value?.ToString()?.Trim();

        if (string.IsNullOrEmpty(valStr))
        {
            return FormatForDevice();
        }

        // 1. If string explicitly contains an absolute offset (e.g. +05:30, Z)
        if (DateTimeOffset.TryParse(valStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedOffset))
        {
            var targetedTime = TimeZoneInfo.ConvertTime(parsedOffset, _resolvedTimeZone);
            return targetedTime.ToString("yyyy-MM-dd HH:mm:ss");
        }

        // 2. If text lacks timezone hints entirely, treat it as already belonging to target timezone
        if (DateTime.TryParse(valStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDateTime))
        {
            var targetedTime = new DateTimeOffset(parsedDateTime, _resolvedTimeZone.GetUtcOffset(parsedDateTime));
            return targetedTime.ToString("yyyy-MM-dd HH:mm:ss");
        }

        return FormatForDevice();
    }

    public string TimezoneName()
    {
        return _resolvedTimeZone.Id;
    }

    public string DeviceTimezoneOption()
    {
        TimeSpan offset = _resolvedTimeZone.GetUtcOffset(Now());
        double totalHours = offset.TotalHours;

        // If it's a perfect round hour offset (e.g., +7 or -5), return it as a clean integer string
        if (offset.Minutes == 0)
        {
            return ((int)totalHours).ToString();
        }

        // If fractional hour (e.g., +5.5 for India), format to two decimals and drop trailing zeros
        string formatted = totalHours.ToString("0.00", CultureInfo.InvariantCulture)
                                     .TrimEnd('0')
                                     .TrimEnd('.');
        return formatted;
    }

    private TimeZoneInfo ResolveTimeZone()
    {
        foreach (var candidate in GetTimezoneCandidates())
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;

            try
            {
                // Modern .NET automatically maps IANA keys (e.g., Asia/Manila) 
                // to Windows keys seamlessly behind the scenes.
                return TimeZoneInfo.FindSystemTimeZoneById(candidate.Trim());
            }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                // Eat exception to test the next backup candidate down the chain
            }
        }
        return TimeZoneInfo.Utc;
    }

    private string?[] GetTimezoneCandidates()
    {
        return new string?[]
        {
                _configuration["zkteco:system_timezone"], // appsettings json nested match
                Environment.GetEnvironmentVariable("TZ"),
                ReadEtcTimezone(),
                ReadLocaltimeSymlinkTimezone(),
                _configuration["app:timezone"]
        };
    }

    private string? ReadEtcTimezone()
    {
        const string path = "/etc/timezone";
        if (!File.Exists(path)) return null;

        try
        {
            string contents = File.ReadAllText(path).Trim();
            return !string.IsNullOrEmpty(contents) ? contents : null;
        }
        catch
        {
            return null;
        }
    }

    private string? ReadLocaltimeSymlinkTimezone()
    {
        const string path = "/etc/localtime";
        if (!File.Exists(path)) return null;

        try
        {
            // .NET core native capability to pull symbolic link destinations
            var fileInfo = new FileInfo(path);
            string? target = fileInfo.LinkTarget;

            if (string.IsNullOrEmpty(target)) return null;

            const string needle = "/usr/share/zoneinfo/";
            int position = target.IndexOf(needle, StringComparison.Ordinal);
            if (position == -1) return null;

            string timezone = target.Substring(position + needle.Length);
            return !string.IsNullOrEmpty(timezone) ? timezone : null;
        }
        catch
        {
            return null;
        }
    }
}
