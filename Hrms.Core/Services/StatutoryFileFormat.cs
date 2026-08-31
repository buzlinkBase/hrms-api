namespace Hrms.Core.Services;

// Shared CSV-field escaping for the SSS R3 / PhilHealth EPRS / Pag-IBIG MCRF electronic
// file generators (SSSContributionService/PHICContributionService/HDMFContributionService).
internal static class StatutoryFileFormat
{
    public static string CsvField(string value) =>
        value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
}
