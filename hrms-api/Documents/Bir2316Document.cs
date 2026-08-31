using Hrms.Api.Documents.Shared;
using Hrms.Domain.Entities;
using Hrms.Domain.ValueObjects;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Hrms.Api.Documents;

// BIR Form 2316 — Certificate of Compensation Payment/Tax Withheld. Per-employee, per-year,
// always printed (there is no electronic submission format for this the way SSS/PhilHealth/
// Pag-IBIG have — 2316 is issued directly to the employee).
public class Bir2316Document : IDocument
{
    private readonly Bir2316Model _data;
    private readonly Company? _company;

    public Bir2316Document(Bir2316Model data, Company? company = null)
    {
        _data = data;
        _company = company;
    }

    public DocumentMetadata GetMetadata() => new DocumentMetadata
    {
        Title = $"BIR 2316 - {_data.FullName} - {_data.Year}",
        Author = ReportDocumentStyle.CompanyName(_company),
        CreationDate = DateTimeOffset.UtcNow,
    };

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.MarginTop(1.5f, Unit.Centimetre);
            page.MarginBottom(1.5f, Unit.Centimetre);
            page.MarginHorizontal(1.5f, Unit.Centimetre);
            page.DefaultTextStyle(x => x.FontSize(9).FontColor(ReportDocumentStyle.TextColor));

            page.Header().Column(col =>
            {
                col.Item().Element(c => ReportHeaderComposer.ComposeHeader(
                    c, _company, "CERTIFICATE OF COMPENSATION PAYMENT / TAX WITHHELD (BIR FORM 2316)",
                    $"CY {_data.Year}", _data.FullName));
                col.Item().PaddingTop(4).Element(ReportHeaderComposer.ComposeComplianceNotice);
            });
            page.Content().PaddingTop(12).Element(ComposeContent);
            page.Footer().Element(ReportHeaderComposer.ComposeFooter);
        });
    }

    void ComposeContent(IContainer c)
    {
        c.Column(col =>
        {
            col.Spacing(10);

            col.Item().Border(1).BorderColor(ReportDocumentStyle.BorderColor).Column(section =>
            {
                SectionHeader(section, "PART I — EMPLOYEE INFORMATION");
                TwoCol(section, ("Employee Name", _data.FullName), ("Employee No.", _data.EmployeeNo));
                TwoCol(section, ("TIN", _data.TIN), ("RDO Code", _data.RDOCode));
                TwoCol(section, ("Civil Status", _data.CivilStatus), ("Address", _data.Address));
            });

            col.Item().Border(1).BorderColor(ReportDocumentStyle.BorderColor).Column(section =>
            {
                SectionHeader(section, "PART II — EMPLOYER INFORMATION");
                TwoCol(section, ("Registered Name", ReportDocumentStyle.CompanyName(_company)), ("TIN", _company?.TIN ?? ""));
                TwoCol(section, ("RDO Code", _company?.RDOCode ?? ""), ("Address", _company?.Address ?? ""));
            });

            col.Item().Border(1).BorderColor(ReportDocumentStyle.BorderColor).Column(section =>
            {
                SectionHeader(section, "PART III — COMPENSATION AND TAX WITHHELD");
                section.Item().Padding(4).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(3);
                        cols.RelativeColumn(1);
                    });

                    AmountRow(table, "Gross Compensation Income", _data.GrossCompensation);
                    AmountRow(table, "Non-Taxable/Exempt Compensation", _data.NonTaxableCompensation);
                    AmountRow(table, "13th Month Pay and Other Benefits", _data.ThirteenthMonthPay);
                    AmountRow(table, "Taxable Compensation Income", _data.TaxableCompensation, bold: true);
                    SubHeaderRow(table, "STATUTORY CONTRIBUTIONS (SSS/PHILHEALTH/PAG-IBIG)");
                    AmountRow(table, "SSS Contributions", _data.TotalSSS);
                    AmountRow(table, "PhilHealth Contributions", _data.TotalPhilHealth);
                    AmountRow(table, "Pag-IBIG Contributions", _data.TotalPagIbig);
                    AmountRow(table, "TOTAL TAX WITHHELD", _data.TotalTaxWithheld, bold: true);
                });
            });

            col.Item().PaddingTop(20).Row(row =>
            {
                row.RelativeItem().Column(sig =>
                {
                    sig.Item().PaddingTop(30).BorderTop(1).BorderColor(ReportDocumentStyle.TextColor).PaddingTop(2)
                        .Text(string.IsNullOrWhiteSpace(_company?.AuthorizedSignatoryName) ? "Authorized Signatory" : _company!.AuthorizedSignatoryName)
                        .Bold();
                    if (!string.IsNullOrWhiteSpace(_company?.AuthorizedSignatoryTitle))
                        sig.Item().Text(_company.AuthorizedSignatoryTitle).FontSize(8).FontColor(ReportDocumentStyle.LabelColor);
                });
                row.RelativeItem().Column(sig =>
                {
                    sig.Item().PaddingTop(30).BorderTop(1).BorderColor(ReportDocumentStyle.TextColor).PaddingTop(2)
                        .Text(_data.FullName).Bold();
                    sig.Item().Text("Employee Signature").FontSize(8).FontColor(ReportDocumentStyle.LabelColor);
                });
            });
        });
    }

    static void SectionHeader(QuestPDF.Fluent.ColumnDescriptor col, string title) =>
        col.Item().Background(ReportDocumentStyle.SectionHeaderBg)
           .BorderBottom(1).BorderColor(ReportDocumentStyle.BorderColor)
           .Padding(5).Text(title).Bold().FontSize(9).FontColor(ReportDocumentStyle.Primary);

    static void TwoCol(QuestPDF.Fluent.ColumnDescriptor col, (string label, string? value) left, (string label, string? value) right) =>
        col.Item().Row(row =>
        {
            row.RelativeItem().Element(c => LabelValue(c, left.label, left.value));
            row.RelativeItem().Element(c => LabelValue(c, right.label, right.value));
        });

    static void LabelValue(IContainer c, string label, string? value) =>
        c.Padding(4).Row(row =>
        {
            row.ConstantItem(110).Text(label).FontColor(ReportDocumentStyle.LabelColor);
            row.RelativeItem().Text(string.IsNullOrWhiteSpace(value) ? "—" : value);
        });

    static void AmountRow(TableDescriptor table, string label, decimal amount, bool bold = false)
    {
        var labelCell = table.Cell().Padding(3);
        var valueCell = table.Cell().Padding(3).AlignRight();
        if (bold)
        {
            labelCell = labelCell.BorderTop(1).BorderColor(ReportDocumentStyle.BorderColor);
            valueCell = valueCell.BorderTop(1).BorderColor(ReportDocumentStyle.BorderColor);
        }
        var labelText = labelCell.Text(label);
        var valueText = valueCell.Text(ReportDocumentStyle.Money(amount));
        if (bold)
        {
            labelText.Bold();
            valueText.Bold();
        }
    }

    static void SubHeaderRow(TableDescriptor table, string title) =>
        table.Cell().ColumnSpan(2).Background(ReportDocumentStyle.TableHeaderBg).Padding(3).Text(title).Bold().FontSize(8);
}
