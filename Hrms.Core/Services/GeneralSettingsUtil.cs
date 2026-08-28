namespace DTR.Core;

public class GeneralSettingsUtil
{
    public static T ParseEnum<T>(string? input, T fallback) where T : struct, Enum
    {
        if (!string.IsNullOrWhiteSpace(input) && Enum.TryParse<T>(input, ignoreCase: true, out var result))
        {
            return result;
        }
        return fallback;
    }
    public static bool ParseBool(string? input, bool fallback)
    {
        if (!string.IsNullOrWhiteSpace(input) && bool.TryParse(input, out var result))
        {
            return result;
        }
        return fallback;
    }
    public static int ParseInt(string? input, int fallback)
    {
        if (!string.IsNullOrWhiteSpace(input) && int.TryParse(input, out var result))
        {
            return result;
        }
        return fallback;
    }
    public static double ParseDouble(string? input, double fallback)
    {
        if (!string.IsNullOrWhiteSpace(input) && double.TryParse(input, out var result))
        {
            return result;
        }
        return fallback;
    }
}