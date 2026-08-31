using Hrms.Api.Documents.Shared;
using Hrms.Domain.Entities;
using Hrms.Domain.ValueObjects;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Hrms.Api.Documents;

// Tabular annual per-employee preview of the BIR Alphalist — same table-report shape as
// PayrollSummaryReportDocument.
public class AlphalistPreviewDocument : IDocument
{
    private readonly List<AlphalistEntryModel> _rows;
    private readonly int _year;
    private readonly Company? _company;

    public AlphalistPreviewDocument(List<AlphalistEntryModel> rows, int year, Company? company = null)
    {
        _rows = rows;
        _year = year;
        _company = company;
    }

    public DocumentMetadata GetMetadata() => new DocumentMetadata
    {
        Title = $"BIR Alphalist - {_year}",
        Author = ReportDocumentStyle.CompanyName(_company),
        CreationDate = DateTimeOffset.UtcNow,
    };

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A3.Landscape());
            page.MarginTop(1.2f, Unit.Centimetre);
            page.MarginBottom(1.2f, Unit.Centimetre);
            page.MarginHorizontal(1.2f, Unit.Centimetre);
            page.DefaultTextStyle(x => x.FontSize(9).FontColor(ReportDocumentStyle.TextColor));

            page.Header().Column(col =>
            {
                col.Item().Element(c => ReportHeaderComposer.ComposeHeader(
                    c, _company, "BIR ALPHALIST OF EMPLOYEES", $"CY {_year}", $"{_rows.Count} employee(s)"));
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
                cols.RelativeColumn(2.4f); // Employee
                cols.RelativeColumn(1.4f); // TIN
                cols.RelativeColumn(1.2f); // Gross
                cols.RelativeColumn(1.2f); // Non-taxable
                cols.RelativeColumn(1.2f); // Taxable
                cols.RelativeColumn(1.1f); // 13th month
                cols.RelativeColumn(1);    // SSS
                cols.RelativeColumn(1);    // PhilHealth
                cols.RelativeColumn(1);    // Pag-IBIG
                cols.RelativeColumn(1.2f); // Tax withheld
            });

            static IContainer HeaderCell(IContainer c) => c.Background(ReportDocumentStyle.TableHeaderBg).Padding(3);
            static IContainer DataCell(IContainer c) => c.BorderBottom(1).BorderColor("#eeeeee").Padding(3);

            table.Header(header =>
            {
                header.Cell().Element(HeaderCell).Text("Employee").Bold();
                header.Cell().Element(HeaderCell).Text("TIN").Bold();
                header.Cell().Element(HeaderCell).AlignRight().Text("Gross Comp.").Bold();
                header.Cell().Element(HeaderCell).AlignRight().Text("Non-Taxable").Bold();
                header.Cell().Element(HeaderCell).AlignRight().Text("Taxable").Bold();
                header.Cell().Element(HeaderCell).AlignRight().Text("13th Month").Bold();
                header.Cell().Element(HeaderCell).AlignRight().Text("SSS").Bold();
                header.Cell().Element(HeaderCell).AlignRight().Text("PhilHealth").Bold();
                header.Cell().Element(HeaderCell).AlignRight().Text("Pag-IBIG").Bold();
                header.Cell().Element(HeaderCell).AlignRight().Text("Tax Withheld").Bold();
            });

            foreach (var r in _rows)
            {
                table.Cell().Element(DataCell).Text(r.FullName);
                table.Cell().Element(DataCell).Text(r.TIN);
                table.Cell().Element(DataCell).AlignRight().Text(ReportDocumentStyle.Money(r.GrossCompensation));
                table.Cell().Element(DataCell).AlignRight().Text(ReportDocumentStyle.Money(r.NonTaxableCompensation));
                table.Cell().Element(DataCell).AlignRight().Text(ReportDocumentStyle.Money(r.TaxableCompensation)).Bold();
                table.Cell().Element(DataCell).AlignRight().Text(ReportDocumentStyle.Money(r.ThirteenthMonthPay));
                table.Cell().Element(DataCell).AlignRight().Text(ReportDocumentStyle.Money(r.TotalSSS));
                table.Cell().Element(DataCell).AlignRight().Text(ReportDocumentStyle.Money(r.TotalPhilHealth));
                table.Cell().Element(DataCell).AlignRight().Text(ReportDocumentStyle.Money(r.TotalPagIbig));
                table.Cell().Element(DataCell).AlignRight().Text(ReportDocumentStyle.Money(r.TotalTaxWithheld)).Bold().FontColor(ReportDocumentStyle.Primary);
            }

            static IContainer TotalCell(IContainer c) => c.BorderTop(1).BorderColor(ReportDocumentStyle.BorderColor).Padding(3);
            table.Cell().Element(TotalCell).Text("TOTAL").Bold();
            table.Cell().Element(TotalCell).Text("");
            table.Cell().Element(TotalCell).AlignRight().Text(ReportDocumentStyle.Money(_rows.Sum(r => r.GrossCompensation))).Bold();
            table.Cell().Element(TotalCell).AlignRight().Text(ReportDocumentStyle.Money(_rows.Sum(r => r.NonTaxableCompensation))).Bold();
            table.Cell().Element(TotalCell).AlignRight().Text(ReportDocumentStyle.Money(_rows.Sum(r => r.TaxableCompensation))).Bold();
            table.Cell().Element(TotalCell).AlignRight().Text(ReportDocumentStyle.Money(_rows.Sum(r => r.ThirteenthMonthPay))).Bold();
            table.Cell().Element(TotalCell).AlignRight().Text(ReportDocumentStyle.Money(_rows.Sum(r => r.TotalSSS))).Bold();
            table.Cell().Element(TotalCell).AlignRight().Text(ReportDocumentStyle.Money(_rows.Sum(r => r.TotalPhilHealth))).Bold();
            table.Cell().Element(TotalCell).AlignRight().Text(ReportDocumentStyle.Money(_rows.Sum(r => r.TotalPagIbig))).Bold();
            table.Cell().Element(TotalCell).AlignRight().Text(ReportDocumentStyle.Money(_rows.Sum(r => r.TotalTaxWithheld))).Bold().FontColor(ReportDocumentStyle.Primary);
        });
    }
}
