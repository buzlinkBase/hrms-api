using Hrms.Domain.Entities;

namespace Hrms.Api.Documents.Shared;

// Color/font constants pulled out of the pattern PayslipDocument/PayrollSummaryReportDocument/
// Employee201Document each independently declare. Used by the new BIR/statutory report
// documents only — the 3 existing documents are left as-is (not refactored to use this).
internal static class ReportDocumentStyle
{
    // Printouts always render in black, regardless of the app's teal-green UI theme — a
    // deliberate choice for print legibility/ink cost, not an oversight. Named "Primary" to
    // match the on-screen accent's role (headings, totals, section rules), just black instead.
    public const string Primary = "#000000";
    public const string TableHeaderBg = "#e8f5f1";
    public const string SectionHeaderBg = "#f5f5f5";
    public const string BorderColor = "#d9d9d9";
    public const string LabelColor = "#666666";
    public const string TextColor = "#1a1a1a";
    public const string WarningColor = "#ad6800";
    public const string WarningBg = "#fffbe6";

    // Never falls back to the product/vendor name ("One Punch HRIS") — printing the software
    // vendor's name as if it were the employer's registered company name on a payslip/BIR/SSS
    // remittance header would be actively wrong, not just cosmetic. An unconfigured tenant sees
    // an honest placeholder instead, pointing at Company Setup > Company Info's own "Company
    // Name" field (the single source of truth every report now reads from).
    public static string CompanyName(Company? company) =>
        string.IsNullOrWhiteSpace(company?.Description) ? "(Company Name Not Set)" : company.Description;

    public static string Money(decimal v) => v.ToString("N2");
}
