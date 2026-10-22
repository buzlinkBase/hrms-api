namespace Hrms.Domain.Entities;

/// <summary>
/// What a company's regional settings are before anyone changes them. The product is for the
/// Philippines, so everything defaults there.
/// </summary>
public static class CompanyDefaults
{
    public const string Country = "Philippines";
    public const string TimeZone = "Asia/Manila";
    public const string Currency = "PHP";
    public const string RestDays = "Sunday";
}

/// <summary>Reads the company's comma-separated default rest days ("Saturday,Sunday").</summary>
public static class CompanyRestDays
{
    /// <summary>The valid weekdays in the order written, without repeats; unknown names are dropped.
    /// Blank means the company has not set any yet, which is the Philippine default (Sunday).</summary>
    public static List<DayName> Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return [DayName.Sunday];
        return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(text => Enum.TryParse<DayName>(text, ignoreCase: true, out var day) ? (DayName?)day : null)
            .Where(day => day.HasValue)
            .Select(day => day!.Value)
            .Distinct()
            .ToList();
    }
}

/// <summary>How a company numbers its employees: not at all (free text) or automatically.</summary>
public static class EmployeeIdSettings
{
    public const string Off = "Off";
    public const string Auto = "Auto";
    public const int DefaultPadding = 4;
    public const int MaxPrefixLength = 10;
    public const int MaxPadding = 8;

    public static bool IsAuto(string? mode) => string.Equals(mode, Auto, StringComparison.OrdinalIgnoreCase);

    /// <summary>Why these settings can't be saved, or null when they can. Off needs nothing else.</summary>
    public static string? Validate(string? mode, string? prefix, int padding)
    {
        if (string.IsNullOrWhiteSpace(mode) || string.Equals(mode, Off, StringComparison.OrdinalIgnoreCase)) return null;
        if (!IsAuto(mode)) return "Employee ID numbering must be Off or Auto.";
        // The prefix is optional (plain 0001 numbers are fine); when given it must be simple.
        var text = prefix?.Trim() ?? "";
        if (text.Length > MaxPrefixLength || !text.All(c => char.IsAsciiLetterOrDigit(c) || c == '-'))
            return $"The prefix can use letters, numbers and dashes only, up to {MaxPrefixLength} characters.";
        if (padding < 1 || padding > MaxPadding) return $"The number length must be between 1 and {MaxPadding}.";
        return null;
    }

    /// <summary>PREFIX-0001, or PREFIX-2026-0001 with the year. No prefix: 0001, or 2026-0001.</summary>
    public static string Format(string? prefix, int padding, bool includeYear, int year, int number)
    {
        var digits = number.ToString().PadLeft(Math.Clamp(padding, 1, MaxPadding), '0');
        var parts = new List<string>(3);
        if (!string.IsNullOrWhiteSpace(prefix)) parts.Add(prefix.Trim());
        if (includeYear) parts.Add(year.ToString());
        parts.Add(digits);
        return string.Join("-", parts);
    }
}

/// <summary>Joins a company's address line and parts into the single line other systems display.</summary>
public static class CompanyAddress
{
    /// <summary>"Street, Barangay, City, Province ZIP, Country" with blank parts left out. The
    /// country is dropped when it is only the default, which would just repeat itself on every address.</summary>
    public static string Compose(string? line, string? barangay, string? city, string? province, string? zip, string? country)
    {
        var area = string.Join(" ", new[] { province?.Trim(), zip?.Trim() }.Where(x => !string.IsNullOrEmpty(x)));
        var countryText = string.Equals(country?.Trim(), CompanyDefaults.Country, StringComparison.OrdinalIgnoreCase)
            ? null
            : country?.Trim();
        return string.Join(", ", new[] { line?.Trim(), barangay?.Trim(), city?.Trim(), area, countryText }
            .Where(x => !string.IsNullOrEmpty(x)));
    }
}
