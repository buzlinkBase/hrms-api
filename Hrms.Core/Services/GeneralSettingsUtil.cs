namespace DTR.Core;

public class GeneralSettingsUtil
{
    public static T ParseEnum<T>(string input, T fallback) where T : struct, Enum
    {
        if (Enum.TryParse<T>(input, ignoreCase: true, out var result))
        {
            return result;
        }
        return fallback;
    }
    public static bool ParseBool(string input, bool fallback)
    {
        if (bool.TryParse(input, out var result))
        {
            return result;
        }
        return fallback;
    }
    public static int ParseInt(string input, int fallback)
    {
        if (int.TryParse(input, out var result))
        {
            return result;
        }
        return fallback;
    }
    public static double ParseDouble(string input, double fallback)
    {
        if (double.TryParse(input, out var result))
        {
            return result;
        }
        return fallback;
    }
}