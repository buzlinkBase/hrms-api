using Hrms.Domain.Entities;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Hrms.Api.Documents.Shared;

// Shared header/footer/compliance-notice composition for the new BIR/statutory report
// documents, matching the Row/Column header layout PayrollSummaryReportDocument/
// PayslipDocument/Employee201Document each already use, but declared once instead of a
// 4th/5th/6th copy.
internal static class ReportHeaderComposer
{
    public static void ComposeHeader(IContainer c, Company? company, string reportTitle, string periodLabel, string? subLabel = null)
    {
        c.BorderBottom(1).BorderColor(ReportDocumentStyle.Primary).PaddingBottom(6).Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text(ReportDocumentStyle.CompanyName(company)).Bold().FontSize(12).FontColor(ReportDocumentStyle.Primary);
                col.Item().Text(reportTitle).FontSize(9).FontColor(ReportDocumentStyle.LabelColor).LetterSpacing(1);
                var identifiers = new[] { company?.TIN is { Length: > 0 } tin ? $"TIN {tin}" : null, company?.Address, company?.Contact }
                    .Where(s => !string.IsNullOrWhiteSpace(s));
                if (identifiers.Any())
                {
                    col.Item().Text(string.Join("  •  ", identifiers)).FontSize(7.5f).FontColor(ReportDocumentStyle.LabelColor);
                }
            });
            row.ConstantItem(220).AlignRight().Column(col =>
            {
                col.Item().Text(periodLabel).Bold().FontSize(10);
                if (!string.IsNullOrWhiteSpace(subLabel))
                    col.Item().Text(subLabel).FontSize(8).FontColor(ReportDocumentStyle.LabelColor);
            });
        });
    }

    public static void ComposeFooter(IContainer c)
    {
        c.AlignCenter().PaddingTop(4).Text(t =>
        {
            t.DefaultTextStyle(x => x.FontSize(8).FontColor(ReportDocumentStyle.LabelColor));
            t.Span("Page ");
            t.CurrentPageNumber();
            t.Span(" of ");
            t.TotalPages();
        });
    }

    // Visible half of the "implemented from general knowledge, not a live agency spec"
    // caveat — shown at the top of every generated BIR/SSS/PhilHealth/Pag-IBIG document so
    // the preparer sees it before relying on the figures/file for an actual submission.
    public static void ComposeComplianceNotice(IContainer c) =>
        c.Background(ReportDocumentStyle.WarningBg)
         .Border(1).BorderColor(ReportDocumentStyle.WarningColor)
         .Padding(6)
         .Text("Format implemented from general knowledge of this agency's requirements — verify against the current portal/spec before an actual submission.")
         .FontSize(7.5f).FontColor(ReportDocumentStyle.WarningColor).Italic();
}
