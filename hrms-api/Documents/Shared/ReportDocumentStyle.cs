using Hrms.Domain.Entities;

namespace Hrms.Api.Documents.Shared;

// Color/font constants pulled out of the pattern PayslipDocument/PayrollSummaryReportDocument/
// Employee201Document each independently declare. Used by the new BIR/statutory report
// documents only — the 3 existing documents are left as-is (not refactored to use this).
internal static class ReportDocumentStyle
{
    public const string Primary = "#1DA081";
    public const string TableHeaderBg = "#e8f5f1";
    public const string SectionHeaderBg = "#f5f5f5";
    public const string BorderColor = "#d9d9d9";
    public const string LabelColor = "#666666";
    public const string TextColor = "#1a1a1a";
    public const string WarningColor = "#ad6800";
    public const string WarningBg = "#fffbe6";

    // Falls back to the product name until the tenant fills in Company Setup — same
    // convention as the 3 existing documents.
    public static string CompanyName(Company? company) =>
        string.IsNullOrWhiteSpace(company?.Description) ? "One Punch HRIS" : company.Description;

    public static string Money(decimal v) => v.ToString("N2");
}
