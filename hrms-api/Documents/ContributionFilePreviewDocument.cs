using Hrms.Api.Documents.Shared;
using Hrms.Domain.Entities;
using Hrms.Domain.ValueObjects;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Hrms.Api.Documents;

// Shared tabular preview for the SSS R3 / PhilHealth EPRS / Pag-IBIG MCRF electronic files —
// all three reuse the same ContributionRemittanceModel shape their respective
// *ContributionService.GetRemittanceReportAsync already returns, so one parameterized
// document covers all three instead of three near-identical files.
public class ContributionFilePreviewDocument : IDocument
{
    private readonly List<ContributionRemittanceModel> _rows;
    private readonly string _reportTitle;
    private readonly string _idLabel;
    private readonly DateOnly _from;
    private readonly DateOnly _to;
    private readonly Company? _company;

    public ContributionFilePreviewDocument(
        List<ContributionRemittanceModel> rows, string reportTitle, string idLabel,
        DateOnly from, DateOnly to, Company? company = null)
    {
        _rows = rows;
        _reportTitle = reportTitle;
        _idLabel = idLabel;
        _from = from;
        _to = to;
        _company = company;
    }

    public DocumentMetadata GetMetadata() => new DocumentMetadata
    {
        Title = $"{_reportTitle} - {_from:MMM dd} to {_to:MMM dd, yyyy}",
        Author = ReportDocumentStyle.CompanyName(_company),
        CreationDate = DateTimeOffset.UtcNow,
    };

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.MarginTop(1.2f, Unit.Centimetre);
            page.MarginBottom(1.2f, Unit.Centimetre);
            page.MarginHorizontal(1.2f, Unit.Centimetre);
            page.DefaultTextStyle(x => x.FontSize(9).FontColor(ReportDocumentStyle.TextColor));

            page.Header().Column(col =>
            {
                col.Item().Element(c => ReportHeaderComposer.ComposeHeader(
                    c, _company, _reportTitle.ToUpperInvariant(),
                    $"{_from:MMM dd} – {_to:MMM dd, yyyy}", $"{_rows.Count} record(s)"));
                col.Item().PaddingTop(4).Element(ReportHeaderComposer.ComposeComplianceNotice);
            });
            page.Content().PaddingTop(8).Element(ComposeTable);
            page.Footer().Element(ReportHeaderComposer.ComposeFooter);
        });
    }

    void ComposeTable(IContainer c)
    {
        c.Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.RelativeColumn(1.4f); // ID number
                cols.RelativeColumn(2.4f); // Employee
                cols.RelativeColumn(1);    // EE Share
                cols.RelativeColumn(1);    // ER Share
                cols.RelativeColumn(1.1f); // Total
            });

            static IContainer HeaderCell(IContainer c) => c.Background(ReportDocumentStyle.TableHeaderBg).Padding(3);
            static IContainer DataCell(IContainer c) => c.BorderBottom(1).BorderColor("#eeeeee").Padding(3);

            table.Header(header =>
            {
                header.Cell().Element(HeaderCell).Text(_idLabel).Bold();
                header.Cell().Element(HeaderCell).Text("Employee").Bold();
                header.Cell().Element(HeaderCell).AlignRight().Text("EE Share").Bold();
                header.Cell().Element(HeaderCell).AlignRight().Text("ER Share").Bold();
                header.Cell().Element(HeaderCell).AlignRight().Text("Total").Bold();
            });

            foreach (var r in _rows)
            {
                table.Cell().Element(DataCell).Text(r.GovIdNumber);
                table.Cell().Element(DataCell).Text(r.FullName);
                table.Cell().Element(DataCell).AlignRight().Text(ReportDocumentStyle.Money(r.EmployeeShare));
                table.Cell().Element(DataCell).AlignRight().Text(ReportDocumentStyle.Money(r.EmployerShare));
                table.Cell().Element(DataCell).AlignRight().Text(ReportDocumentStyle.Money(r.TotalContribution)).Bold();
            }

            static IContainer TotalCell(IContainer c) => c.BorderTop(1).BorderColor(ReportDocumentStyle.BorderColor).Padding(3);
            table.Cell().Element(TotalCell).Text("TOTAL").Bold();
            table.Cell().Element(TotalCell).Text("");
            table.Cell().Element(TotalCell).AlignRight().Text(ReportDocumentStyle.Money(_rows.Sum(r => r.EmployeeShare))).Bold();
            table.Cell().Element(TotalCell).AlignRight().Text(ReportDocumentStyle.Money(_rows.Sum(r => r.EmployerShare))).Bold();
            table.Cell().Element(TotalCell).AlignRight().Text(ReportDocumentStyle.Money(_rows.Sum(r => r.TotalContribution))).Bold().FontColor(ReportDocumentStyle.Primary);
        });
    }
}
