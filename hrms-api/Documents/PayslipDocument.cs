using Hrms.Domain.Entities;
using Hrms.Domain.ValueObjects;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Hrms.Api.Documents;

public class PayslipDocument : IDocument
{
    private readonly Payroll _p;
    private readonly EmployeeFullModel _e;

    private static readonly string Primary = "#1DA081";
    private static readonly string SectionHeaderBg = "#f5f5f5";
    private static readonly string SubHeaderBg = "#e8f5f1";
    private static readonly string BorderColor = "#d9d9d9";
    private static readonly string LabelColor = "#666666";
    private static readonly string TextColor = "#1a1a1a";

    public PayslipDocument(Payroll payroll, EmployeeFullModel employee)
    {
        _p = payroll;
        _e = employee;
    }

    public DocumentMetadata GetMetadata() => new DocumentMetadata
    {
        Title = $"Payslip - {_e.FullName} - {_p.PayPeriodStart:MMM dd} to {_p.PayPeriodEnd:MMM dd, yyyy}",
        Author = "One Punch HRIS",
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
            page.DefaultTextStyle(x => x.FontSize(9).FontColor(TextColor));

            page.Header().Element(ComposePageHeader);
            page.Content().PaddingTop(8).Element(ComposeContent);
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
                col.Item().Text("ONE PUNCH HRIS").Bold().FontSize(12).FontColor(Primary);
                col.Item().Text("PAYSLIP").FontSize(9).FontColor(LabelColor).LetterSpacing(1);
            });
            row.ConstantItem(200).AlignRight().Column(col =>
            {
                col.Item().Text(_e.FullName ?? "—").Bold().FontSize(11);
                col.Item().Text($"#{_e.EmployeeNo}").FontSize(9).FontColor(LabelColor);
                col.Item().Text(
                    $"{_p.PayPeriodStart:MMM dd} – {_p.PayPeriodEnd:MMM dd, yyyy}")
                    .FontSize(9).FontColor(Primary);
            });
        });
    }

    void ComposeContent(IContainer c)
    {
        c.Column(col =>
        {
            col.Spacing(8);
            col.Item().Element(ComposeEmployeeInfo);
            col.Item().Element(ComposeEarnings);
            col.Item().Element(ComposeDeductions);
            col.Item().Element(ComposeNetPay);
        });
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    static string Money(decimal v) => v.ToString("N2");

    void SectionHeader(IContainer c, string title) =>
        c.Background(SectionHeaderBg)
         .BorderBottom(1).BorderColor(BorderColor)
         .Padding(5)
         .Text(title).Bold().FontSize(9).FontColor(Primary);

    void LabelValue(IContainer c, string label, string? value) =>
        c.Padding(3).Row(row =>
        {
            row.ConstantItem(110).Text(label).FontColor(LabelColor);
            row.RelativeItem().Text(value ?? "—");
        });

    void TwoColRow(QuestPDF.Fluent.ColumnDescriptor col, (string label, string? value) left, (string label, string? value) right) =>
        col.Item().Row(row =>
        {
            row.RelativeItem().Element(c => LabelValue(c, left.label, left.value));
            row.RelativeItem().Element(c => LabelValue(c, right.label, right.value));
        });

    // Amount row helper for the earnings/deductions tables — a plain line item, a
    // sub-section header (spans both columns), or a bold ruled-off total.
    static void AmountRow(TableDescriptor table, string label, decimal amount, bool bold = false)
    {
        var labelCell = table.Cell().Padding(3);
        var valueCell = table.Cell().Padding(3).AlignRight();
        if (bold)
        {
            labelCell = labelCell.BorderTop(1).BorderColor(BorderColor);
            valueCell = valueCell.BorderTop(1).BorderColor(BorderColor);
        }
        var labelText = labelCell.Text(label);
        var valueText = valueCell.Text(Money(amount));
        if (bold)
        {
            labelText.Bold();
            valueText.Bold();
        }
    }

    static void SubHeaderRow(TableDescriptor table, string title) =>
        table.Cell().ColumnSpan(2).Background(SubHeaderBg).Padding(3).Text(title).Bold().FontSize(8);

    // ── Sections ─────────────────────────────────────────────────────────────

    void ComposeEmployeeInfo(IContainer c)
    {
        c.Border(1).BorderColor(BorderColor).Column(col =>
        {
            col.Item().Element(c2 => SectionHeader(c2, "EMPLOYEE INFORMATION"));
            TwoColRow(col,
                ("Position", _e.PositionName),
                ("Department", _e.DepartmentName));
            TwoColRow(col,
                ("Client / Site", _e.ClientName),
                ("Payroll Group", _e.PayrollGroupName));
            TwoColRow(col,
                ("Salary Type", _p.SalaryType.ToString()),
                ("Daily Rate", _p.DailyRate > 0 ? Money(_p.DailyRate) : "—"));
            TwoColRow(col,
                ("Payroll Date", _p.PayrollDate.ToString("MMM dd, yyyy")),
                ("Pay Date", _p.PayDate?.ToString("MMM dd, yyyy") ?? "—"));
        });
    }

    void ComposeEarnings(IContainer c)
    {
        c.Border(1).BorderColor(BorderColor).Column(col =>
        {
            col.Item().Element(c2 => SectionHeader(c2, "EARNINGS"));
            col.Item().Padding(4).Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(3);
                    cols.RelativeColumn(1);
                });

                AmountRow(table, "Basic Pay", _p.BasicPay);
                AmountRow(table, "Regular Overtime", _p.RegularOTPay);
                AmountRow(table, "Regular Night Differential", _p.RegularNDPay);
                AmountRow(table, "Regular Night Diff. Overtime", _p.RegularNDOTPay);
                AmountRow(table, "Rest Day",
                    _p.RestDayPay + _p.RestDayOTPay + _p.RestDayNDPay + _p.RestDayNDOTPay);

                SubHeaderRow(table, "HOLIDAY BREAKDOWN");
                AmountRow(table, "Legal Holiday (Unworked)", _p.LegalHolidayUnworkedPay);
                AmountRow(table, "Holiday Duty (Worked)",
                    (_p.LegalPay - _p.LegalHolidayUnworkedPay) + _p.LegalOTPay + _p.LegalNDPay + _p.LegalNDOTPay);
                AmountRow(table, "Rest Day + Legal Holiday",
                    _p.RestLegalPay + _p.RestLegalOTPay + _p.RestLegalNDPay + _p.RestLegalNDOTPay);
                AmountRow(table, "Rest Day + Special Holiday",
                    _p.RestSpecialPay + _p.RestSpecialOTPay + _p.RestSpecialNDPay + _p.RestSpecialNDOTPay);
                AmountRow(table, "Special Holiday",
                    _p.SpecialPay + _p.SpecialOTPay + _p.SpecialNDPay + _p.SpecialNDOTPay);
                AmountRow(table, "Double Legal Holiday",
                    _p.DoubleLegalPay + _p.DoubleLegalOTPay + _p.DoubleLegalNDPay + _p.DoubleLegalNDOTPay +
                    _p.RestDoubleLegalPay + _p.RestDoubleLegalOTPay + _p.RestDoubleLegalNDPay + _p.RestDoubleLegalNDOTPay);

                SubHeaderRow(table, "OTHER INCOME");
                AmountRow(table, "COLA", _p.Cola);
                AmountRow(table, "Regular Allowances", _p.TotalRegularAllowances);
                AmountRow(table, "Bonuses", _p.TotalBonuses);
                AmountRow(table, "Commissions", _p.TotalCommissions);
                AmountRow(table, "De Minimis", _p.TotalDeminimises);
                AmountRow(table, "Reimbursement", _p.Reimbursement);
                AmountRow(table, "Other Income", _p.TotalOtherIncome);

                AmountRow(table, "GROSS INCOME", _p.GrossIncome, bold: true);
            });
        });
    }

    void ComposeDeductions(IContainer c)
    {
        c.Border(1).BorderColor(BorderColor).Column(col =>
        {
            col.Item().Element(c2 => SectionHeader(c2, "DEDUCTIONS"));
            col.Item().Padding(4).Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(3);
                    cols.RelativeColumn(1);
                });

                AmountRow(table, "SSS Contribution", _p.SSSContribution);
                AmountRow(table, "PhilHealth Contribution", _p.PhilHealthContribution);
                AmountRow(table, "Pag-IBIG Contribution", _p.PagIbigContribution);
                AmountRow(table, "Withholding Tax", _p.WithholdingTax);
                AmountRow(table, "Loans", _p.TotalLoans);
                AmountRow(table, "Other Deductions", _p.OtherDeductions - _p.TotalLoans);
                AmountRow(table, "Late", _p.LateAmount);
                AmountRow(table, "Under Time", _p.UnderTimeAmount);
                AmountRow(table, "Absences", _p.AbsencesAmount);

                AmountRow(table, "TOTAL DEDUCTIONS", _p.TotalDeductions, bold: true);
            });
        });
    }

    void ComposeNetPay(IContainer c)
    {
        c.Background(SubHeaderBg).Border(1).BorderColor(Primary).Padding(10).Row(row =>
        {
            row.RelativeItem().Text("NET PAY").Bold().FontSize(12).FontColor(Primary);
            row.ConstantItem(150).AlignRight().Text(Money(_p.NetPay)).Bold().FontSize(14).FontColor(Primary);
        });
    }
}
