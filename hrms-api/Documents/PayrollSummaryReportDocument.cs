using Hrms.Domain.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Hrms.Api.Documents;

public class PayrollSummaryReportDocument : IDocument
{
    private readonly List<Payroll> _rows;
    private readonly DateOnly _from;
    private readonly DateOnly _to;
    private readonly Company? _company;

    private static readonly string Primary = "#1DA081";
    private static readonly string TableHeaderBg = "#e8f5f1";
    private static readonly string BorderColor = "#d9d9d9";
    private static readonly string LabelColor = "#666666";
    private static readonly string TextColor = "#1a1a1a";

    // Falls back to the product name until the tenant fills in Company Setup.
    private string CompanyName => string.IsNullOrWhiteSpace(_company?.Description) ? "One Punch HRIS" : _company.Description;

    public PayrollSummaryReportDocument(List<Payroll> rows, DateOnly from, DateOnly to, Company? company = null)
    {
        _rows = rows;
        _from = from;
        _to = to;
        _company = company;
    }

    public DocumentMetadata GetMetadata() => new DocumentMetadata
    {
        Title = $"Payroll Summary - {_from:MMM dd} to {_to:MMM dd, yyyy}",
        Author = CompanyName,
        CreationDate = DateTimeOffset.UtcNow,
    };

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            // A3 landscape (rather than A4) so all 15 columns fit with legible font sizes
            // instead of being crushed down to fit A4's narrower width.
            page.Size(PageSizes.A3.Landscape());
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
                col.Item().Text("PAYROLL SUMMARY REPORT").FontSize(9).FontColor(LabelColor);
                if (!string.IsNullOrWhiteSpace(_company?.Address) || !string.IsNullOrWhiteSpace(_company?.Contact))
                {
                    col.Item().Text(string.Join("  •  ", new[] { _company?.Address, _company?.Contact }
                        .Where(s => !string.IsNullOrWhiteSpace(s))))
                        .FontSize(7.5f).FontColor(LabelColor);
                }
            });
            row.ConstantItem(220).AlignRight().Column(col =>
            {
                col.Item().Text($"{_from:MMM dd} – {_to:MMM dd, yyyy}").Bold().FontSize(10);
                col.Item().Text($"{_rows.Count} record(s)").FontSize(8).FontColor(LabelColor);
            });
        });
    }

    void ComposeTable(IContainer c)
    {
        static string Money(decimal v) => v.ToString("N2");

        c.Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.RelativeColumn(2.4f); // Employee
                cols.RelativeColumn(1);    // Basic
                cols.RelativeColumn(1);    // OT
                cols.RelativeColumn(1);    // ND
                cols.RelativeColumn(1);    // Holiday
                cols.RelativeColumn(1);    // Allowances
                cols.RelativeColumn(1);    // Other Income
                cols.RelativeColumn(1);    // One-Time Payout (Company)
                cols.RelativeColumn(1);    // One-Time Payout (Government)
                cols.RelativeColumn(1.1f); // Gross
                cols.RelativeColumn(1);    // SSS
                cols.RelativeColumn(1);    // PhilHealth
                cols.RelativeColumn(1);    // Pag-IBIG
                cols.RelativeColumn(1);    // W-Tax
                cols.RelativeColumn(1);    // Loans
                cols.RelativeColumn(1);    // Other Ded
                cols.RelativeColumn(1.2f); // Net Pay
            });

            static IContainer HeaderCell(IContainer c) =>
                c.Background(TableHeaderBg).Padding(3);
            static IContainer DataCell(IContainer c) =>
                c.BorderBottom(1).BorderColor("#eeeeee").Padding(3);

            table.Header(header =>
            {
                header.Cell().Element(HeaderCell).Text("Employee").Bold();
                header.Cell().Element(HeaderCell).AlignRight().Text("Basic").Bold();
                header.Cell().Element(HeaderCell).AlignRight().Text("OT").Bold();
                header.Cell().Element(HeaderCell).AlignRight().Text("ND").Bold();
                header.Cell().Element(HeaderCell).AlignRight().Text("Holiday").Bold();
                header.Cell().Element(HeaderCell).AlignRight().Text("Allow.").Bold();
                header.Cell().Element(HeaderCell).AlignRight().Text("Other Inc.").Bold();
                header.Cell().Element(HeaderCell).AlignRight().Text("1x Payout (Co)").Bold();
                header.Cell().Element(HeaderCell).AlignRight().Text("1x Payout (Gov)").Bold();
                header.Cell().Element(HeaderCell).AlignRight().Text("Gross").Bold();
                header.Cell().Element(HeaderCell).AlignRight().Text("SSS").Bold();
                header.Cell().Element(HeaderCell).AlignRight().Text("PhilHealth").Bold();
                header.Cell().Element(HeaderCell).AlignRight().Text("Pag-IBIG").Bold();
                header.Cell().Element(HeaderCell).AlignRight().Text("W-Tax").Bold();
                header.Cell().Element(HeaderCell).AlignRight().Text("Loans").Bold();
                header.Cell().Element(HeaderCell).AlignRight().Text("Other Ded.").Bold();
                header.Cell().Element(HeaderCell).AlignRight().Text("Net Pay").Bold();
            });

            foreach (var r in _rows)
            {
                table.Cell().Element(DataCell).Text(r.FullName);
                table.Cell().Element(DataCell).AlignRight().Text(Money(r.BasicPay));
                table.Cell().Element(DataCell).AlignRight().Text(Money(r.OvertimePay));
                table.Cell().Element(DataCell).AlignRight().Text(Money(r.NightDifferentialPay));
                table.Cell().Element(DataCell).AlignRight().Text(Money(r.HolidayPay));
                table.Cell().Element(DataCell).AlignRight().Text(Money(r.TotalRegularAllowances));
                table.Cell().Element(DataCell).AlignRight().Text(Money(r.TotalOtherIncome));
                table.Cell().Element(DataCell).AlignRight().Text(Money(r.CompanyFundedLeavePay));
                table.Cell().Element(DataCell).AlignRight().Text(Money(r.GovernmentFundedLeavePay));
                table.Cell().Element(DataCell).AlignRight().Text(Money(r.GrossIncome)).Bold();
                table.Cell().Element(DataCell).AlignRight().Text(Money(r.SSSContribution));
                table.Cell().Element(DataCell).AlignRight().Text(Money(r.PhilHealthContribution));
                table.Cell().Element(DataCell).AlignRight().Text(Money(r.PagIbigContribution));
                table.Cell().Element(DataCell).AlignRight().Text(Money(r.WithholdingTax));
                table.Cell().Element(DataCell).AlignRight().Text(Money(r.TotalLoans));
                table.Cell().Element(DataCell).AlignRight().Text(Money(r.OtherDeductions - r.TotalLoans));
                table.Cell().Element(DataCell).AlignRight().Text(Money(r.NetPay)).Bold().FontColor(Primary);
            }

            static IContainer TotalCell(IContainer c) =>
                c.BorderTop(1).BorderColor(BorderColor).Padding(3);

            table.Cell().Element(TotalCell).Text("TOTAL").Bold();
            table.Cell().Element(TotalCell).AlignRight().Text(Money(_rows.Sum(r => r.BasicPay))).Bold();
            table.Cell().Element(TotalCell).AlignRight().Text(Money(_rows.Sum(r => r.OvertimePay))).Bold();
            table.Cell().Element(TotalCell).AlignRight().Text(Money(_rows.Sum(r => r.NightDifferentialPay))).Bold();
            table.Cell().Element(TotalCell).AlignRight().Text(Money(_rows.Sum(r => r.HolidayPay))).Bold();
            table.Cell().Element(TotalCell).AlignRight().Text(Money(_rows.Sum(r => r.TotalRegularAllowances))).Bold();
            table.Cell().Element(TotalCell).AlignRight().Text(Money(_rows.Sum(r => r.TotalOtherIncome))).Bold();
            table.Cell().Element(TotalCell).AlignRight().Text(Money(_rows.Sum(r => r.CompanyFundedLeavePay))).Bold();
            table.Cell().Element(TotalCell).AlignRight().Text(Money(_rows.Sum(r => r.GovernmentFundedLeavePay))).Bold();
            table.Cell().Element(TotalCell).AlignRight().Text(Money(_rows.Sum(r => r.GrossIncome))).Bold();
            table.Cell().Element(TotalCell).AlignRight().Text(Money(_rows.Sum(r => r.SSSContribution))).Bold();
            table.Cell().Element(TotalCell).AlignRight().Text(Money(_rows.Sum(r => r.PhilHealthContribution))).Bold();
            table.Cell().Element(TotalCell).AlignRight().Text(Money(_rows.Sum(r => r.PagIbigContribution))).Bold();
            table.Cell().Element(TotalCell).AlignRight().Text(Money(_rows.Sum(r => r.WithholdingTax))).Bold();
            table.Cell().Element(TotalCell).AlignRight().Text(Money(_rows.Sum(r => r.TotalLoans))).Bold();
            table.Cell().Element(TotalCell).AlignRight().Text(Money(_rows.Sum(r => r.OtherDeductions - r.TotalLoans))).Bold();
            table.Cell().Element(TotalCell).AlignRight().Text(Money(_rows.Sum(r => r.NetPay))).Bold().FontColor(Primary);
        });
    }
}
