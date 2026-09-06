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
    private readonly List<MonthlyRemittanceReturnEmployeeModel> _employees;
    private readonly Company? _company;

    public MonthlyRemittanceReturnDocument(
        MonthlyRemittanceReturnModel data,
        List<MonthlyRemittanceReturnEmployeeModel> employees,
        Company? company = null)
    {
        _data = data;
        _employees = employees;
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
                    $"{_data.PeriodFrom:MMM dd} – {_data.PeriodTo:MMM dd, yyyy}"
                        + (_data.AmendedReturn ? "  —  AMENDED RETURN" : "")));
                col.Item().PaddingTop(4).Element(ReportHeaderComposer.ComposeComplianceNotice);
            });
            page.Content().PaddingTop(12).Element(ComposeContent);
            page.Footer().Element(ReportHeaderComposer.ComposeFooter);
        });
    }

    void ComposeContent(IContainer c)
    {
        static IContainer HeaderCell(IContainer c) => c.Background(ReportDocumentStyle.TableHeaderBg).Padding(3);
        static IContainer DataCell(IContainer c) => c.BorderBottom(1).BorderColor("#eeeeee").Padding(3);

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
                LabelValue(section, "Line 15 — Total Amount of Compensation", ReportDocumentStyle.Money(_data.Line15_TotalCompensation));
                LabelValue(section, "Line 16A — Statutory Minimum Wage (MWEs)", ReportDocumentStyle.Money(_data.Line16A_StatutoryMinimumWage));
                LabelValue(section, "Line 16B — Holiday/OT/Hazard/Night Diff Pay (MWEs)", ReportDocumentStyle.Money(_data.Line16B_MWEPremiumPay));
                LabelValue(section, "Line 16C — Other Non-Taxable Compensation", ReportDocumentStyle.Money(_data.Line16C_OtherNonTaxable));
                LabelValue(section, "Line 17 — Total Non-Taxable Compensation", ReportDocumentStyle.Money(_data.Line17_TotalNonTaxable));
                LabelValue(section, "Line 18 — Taxable Compensation", ReportDocumentStyle.Money(_data.Line18_TaxableCompensation));
                section.Item().Background(ReportDocumentStyle.TableHeaderBg).Padding(6).Row(row =>
                {
                    row.RelativeItem().Text("Line 19 — Tax Required to be Withheld").Bold().FontColor(ReportDocumentStyle.Primary);
                    row.ConstantItem(140).AlignRight().Text(ReportDocumentStyle.Money(_data.Line19_TaxWithheld)).Bold().FontSize(12).FontColor(ReportDocumentStyle.Primary);
                });
            });

            if (_data.HasUnwithheldTaxWarning)
                col.Item().Background(ReportDocumentStyle.WarningBg).Padding(6)
                    .Text("Warning: Taxable Compensation is greater than ₱0.00 but Tax Withheld is ₱0.00 — check for missing withholding.")
                    .FontColor(ReportDocumentStyle.WarningColor).FontSize(8);

            if (_data.UnclassifiedEmployeeCount > 0)
                col.Item().Background(ReportDocumentStyle.WarningBg).Padding(6)
                    .Text($"Warning: {_data.UnclassifiedEmployeeCount} employee(s) could not be classified as Minimum Wage Earner or not "
                        + "(missing Branch/Region/Minimum Wage Rate setup) — defaulted to regular (KR010). Review before filing.")
                    .FontColor(ReportDocumentStyle.WarningColor).FontSize(8);

            col.Item().Border(1).BorderColor(ReportDocumentStyle.BorderColor).Column(section =>
            {
                section.Item().Background(ReportDocumentStyle.SectionHeaderBg)
                    .BorderBottom(1).BorderColor(ReportDocumentStyle.BorderColor).Padding(5)
                    .Text("PER-EMPLOYEE BREAKDOWN").Bold().FontSize(9).FontColor(ReportDocumentStyle.Primary);
                section.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(3);
                        c.RelativeColumn(1.2f);
                        c.RelativeColumn(1.5f);
                        c.RelativeColumn(1.5f);
                        c.RelativeColumn(1.5f);
                    });
                    table.Header(h =>
                    {
                        h.Cell().Element(HeaderCell).Text("Employee");
                        h.Cell().Element(HeaderCell).Text("ATC");
                        h.Cell().Element(HeaderCell).AlignRight().Text("Gross Comp.");
                        h.Cell().Element(HeaderCell).AlignRight().Text("Non-Taxable");
                        h.Cell().Element(HeaderCell).AlignRight().Text("Tax Withheld");
                    });
                    foreach (var e in _employees)
                    {
                        table.Cell().Element(DataCell).Text($"{e.FullName} ({e.EmployeeNo}){(e.IsUnclassified ? " *" : "")}");
                        table.Cell().Element(DataCell).Text(e.AtcCode);
                        table.Cell().Element(DataCell).AlignRight().Text(ReportDocumentStyle.Money(e.GrossCompensation));
                        table.Cell().Element(DataCell).AlignRight().Text(ReportDocumentStyle.Money(e.StatutoryMinimumWage + e.MWEPremiumPay + e.OtherNonTaxable));
                        table.Cell().Element(DataCell).AlignRight().Text(ReportDocumentStyle.Money(e.TaxWithheld));
                    }
                });
                if (_data.UnclassifiedEmployeeCount > 0)
                    section.Item().Padding(5)
                        .Text("* Minimum Wage Earner status could not be determined for this employee (missing Branch/Region/Minimum Wage Rate) — defaulted to KR010.")
                        .FontSize(7).FontColor(ReportDocumentStyle.LabelColor);
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
