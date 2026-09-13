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

    // Returns null (not just the fallback concept) when input is missing/unparseable/<= 0 — the
    // caller-facing "no value set" state for a cap, since 0 and "not set" mean the same thing
    // (see StatutoryCapHelper.ApplyClientCap).
    public static decimal? ParsePositiveDecimalOrNull(string? input)
    {
        if (!string.IsNullOrWhiteSpace(input) && decimal.TryParse(input, out var result) && result > 0)
        {
            return result;
        }
        return null;
    }
}