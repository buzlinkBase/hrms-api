using Hrms.Api.Documents.Shared;
using Hrms.Domain.Entities;
using Hrms.Domain.ValueObjects;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Hrms.Api.Documents;

// BIR Form 1601-C summary — not a submittable file (BIR doesn't accept a raw upload for
// this return; filing happens through eBIRForms/eFPS), so this exists to give the preparer
// the exact figures to transcribe.
public class MonthlyRemittanceReturnDocument : IDocument
{
    private readonly MonthlyRemittanceReturnModel _data;
    private readonly Company? _company;

    public MonthlyRemittanceReturnDocument(MonthlyRemittanceReturnModel data, Company? company = null)
    {
        _data = data;
        _company = company;
    }

    public DocumentMetadata GetMetadata() => new DocumentMetadata
    {
        Title = $"BIR 1601-C - {_data.PeriodFrom:MMM yyyy}",
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
                    c, _company, "MONTHLY REMITTANCE RETURN OF INCOME TAXES WITHHELD ON COMPENSATION (BIR FORM 1601-C)",
                    $"{_data.PeriodFrom:MMM dd} – {_data.PeriodTo:MMM dd, yyyy}"));
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
                section.Item().Background(ReportDocumentStyle.SectionHeaderBg)
                    .BorderBottom(1).BorderColor(ReportDocumentStyle.BorderColor).Padding(5)
                    .Text("WITHHOLDING AGENT").Bold().FontSize(9).FontColor(ReportDocumentStyle.Primary);
                LabelValue(section, "Registered Name", ReportDocumentStyle.CompanyName(_company));
                LabelValue(section, "TIN", _company?.TIN);
                LabelValue(section, "RDO Code", _company?.RDOCode);
            });

            col.Item().Border(1).BorderColor(ReportDocumentStyle.BorderColor).Column(section =>
            {
                section.Item().Background(ReportDocumentStyle.SectionHeaderBg)
                    .BorderBottom(1).BorderColor(ReportDocumentStyle.BorderColor).Padding(5)
                    .Text("RETURN FIGURES").Bold().FontSize(9).FontColor(ReportDocumentStyle.Primary);
                LabelValue(section, "Number of Employees", _data.EmployeeCount.ToString());
                LabelValue(section, "Total Taxable Compensation (Schedule 1)", ReportDocumentStyle.Money(_data.TotalTaxableCompensation));
                section.Item().Background(ReportDocumentStyle.TableHeaderBg).Padding(6).Row(row =>
                {
                    row.RelativeItem().Text("Total Tax Withheld (Line 21 / Amount of Tax Withheld)").Bold().FontColor(ReportDocumentStyle.Primary);
                    row.ConstantItem(140).AlignRight().Text(ReportDocumentStyle.Money(_data.TotalTaxWithheld)).Bold().FontSize(12).FontColor(ReportDocumentStyle.Primary);
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
            });
        });
    }

    static void LabelValue(QuestPDF.Fluent.ColumnDescriptor col, string label, string? value) =>
        col.Item().Padding(5).Row(row =>
        {
            row.ConstantItem(220).Text(label).FontColor(ReportDocumentStyle.LabelColor);
            row.RelativeItem().Text(string.IsNullOrWhiteSpace(value) ? "—" : value);
        });
}
