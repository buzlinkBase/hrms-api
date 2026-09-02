using Hrms.Domain.Entities;
using Hrms.Domain.ValueObjects;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Hrms.Api.Documents;

public class ThirteenthMonthPayListDocument : IDocument
{
    private readonly List<ThirteenthMonthModel> _rows;
    private readonly int _year;
    private readonly Company? _company;

    private static readonly string Primary = "#1DA081";
    private static readonly string TableHeaderBg = "#e8f5f1";
    private static readonly string BorderColor = "#d9d9d9";
    private static readonly string LabelColor = "#666666";
    private static readonly string TextColor = "#1a1a1a";

    // Falls back to the product name until the tenant fills in Company Setup.
    private string CompanyName => string.IsNullOrWhiteSpace(_company?.Description) ? "One Punch HRIS" : _company.Description;

    public ThirteenthMonthPayListDocument(List<ThirteenthMonthModel> rows, int year, Company? company = null)
    {
        _rows = rows;
        _year = year;
        _company = company;
    }

    public DocumentMetadata GetMetadata() => new DocumentMetadata
    {
        Title = $"13th Month Pay - {_year}",
        Author = CompanyName,
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
            page.DefaultTextStyle(x => x.FontSize(9).FontColor(TextColor));

            page.Header().Element(ComposePageHeader);
            page.Content().PaddingTop(8).Element(ComposeTable);
            page.Footer().AlignCenter().PaddingTop(4).Text(t =>
            {
                t.DefaultTextStyle(x => x.FontSize(8).FontColor(LabelColor));
                t.Span("Page ");
                t.CurrentPageNumber();
                t.Span(" of ");
                t.TotalPages();
            });
        });
    }

    void ComposePageHeader(IContainer c)
    {
        c.BorderBottom(1).BorderColor(Primary).PaddingBottom(6).Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text(CompanyName).Bold().FontSize(12).FontColor(Primary);
                col.Item().Text("13TH MONTH PAY — LISTING").FontSize(9).FontColor(LabelColor);
                if (!string.IsNullOrWhiteSpace(_company?.Address) || !string.IsNullOrWhiteSpace(_company?.Contact))
                {
                    col.Item().Text(string.Join("  •  ", new[] { _company?.Address, _company?.Contact }
                        .Where(s => !string.IsNullOrWhiteSpace(s))))
                        .FontSize(7.5f).FontColor(LabelColor);
                }
            });
            row.ConstantItem(160).AlignRight().Column(col =>
            {
                col.Item().Text(_year.ToString()).Bold().FontSize(14).FontColor(Primary);
                col.Item().Text($"{_rows.Count} employee(s)").FontSize(8).FontColor(LabelColor);
            });
        });
    }

    static string StatusLabel(string status) => status switch
    {
        "Posted" => "Posted",
        "Draft" => "Draft",
        _ => "Not Generated",
    };

    void ComposeTable(IContainer c)
    {
        static string Money(decimal v) => v.ToString("N2");

        c.Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.RelativeColumn(1.2f); // Employee No
                cols.RelativeColumn(2.6f); // Full Name
                cols.RelativeColumn(1.3f); // Total Basic Pay
                cols.RelativeColumn(1.3f); // Special Bonuses
                cols.RelativeColumn(1.3f); // 13th Month Pay
                cols.RelativeColumn(1);    // Status
                cols.RelativeColumn(1.3f); // Net Pay
            });

            static IContainer HeaderCell(IContainer c) =>
                c.Background(TableHeaderBg).Padding(3);
            static IContainer DataCell(IContainer c) =>
                c.BorderBottom(1).BorderColor("#eeeeee").Padding(3);

            table.Header(header =>
            {
                header.Cell().Element(HeaderCell).Text("Employee No").Bold();
                header.Cell().Element(HeaderCell).Text("Full Name").Bold();
                header.Cell().Element(HeaderCell).AlignRight().Text("Total Basic Pay").Bold();
                header.Cell().Element(HeaderCell).AlignRight().Text("Special Bonuses").Bold();
                header.Cell().Element(HeaderCell).AlignRight().Text("13th Month Pay").Bold();
                header.Cell().Element(HeaderCell).Text("Status").Bold();
                header.Cell().Element(HeaderCell).AlignRight().Text("Net Pay").Bold();
            });

            foreach (var r in _rows)
            {
                table.Cell().Element(DataCell).Text(r.EmployeeNo);
                table.Cell().Element(DataCell).Text(r.FullName);
                table.Cell().Element(DataCell).AlignRight().Text(Money(r.TotalBasicPayForYear));
                table.Cell().Element(DataCell).AlignRight().Text(Money(r.TotalSpecialBonusesForYear));
                table.Cell().Element(DataCell).AlignRight().Text(Money(r.ThirteenthMonthPay)).Bold();
                table.Cell().Element(DataCell).Text(StatusLabel(r.Status));
                table.Cell().Element(DataCell).AlignRight().Text(r.NetPay.HasValue ? Money(r.NetPay.Value) : "—").Bold().FontColor(Primary);
            }

            static IContainer TotalCell(IContainer c) =>
                c.BorderTop(1).BorderColor(BorderColor).Padding(3);

            table.Cell().Element(TotalCell).Text("TOTAL").Bold();
            table.Cell().Element(TotalCell).Text("");
            table.Cell().Element(TotalCell).AlignRight().Text(Money(_rows.Sum(r => r.TotalBasicPayForYear))).Bold();
            table.Cell().Element(TotalCell).AlignRight().Text(Money(_rows.Sum(r => r.TotalSpecialBonusesForYear))).Bold();
            table.Cell().Element(TotalCell).AlignRight().Text(Money(_rows.Sum(r => r.ThirteenthMonthPay))).Bold();
            table.Cell().Element(TotalCell).Text("");
            table.Cell().Element(TotalCell).AlignRight().Text(Money(_rows.Sum(r => r.NetPay ?? 0))).Bold().FontColor(Primary);
        });
    }
}
