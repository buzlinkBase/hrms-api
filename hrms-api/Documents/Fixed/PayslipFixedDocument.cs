using Hrms.Api.Documents.Shared;
using Hrms.Domain.Entities;
using Hrms.Domain.ValueObjects;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Hrms.Api.Documents.Fixed;

// Payslip for FIXED-salary employees. They are paid a flat period share of the monthly rate, not
// by hours worked, so there is no "hours x rate" anywhere: Earnings starts from Basic Salary, shows
// the late/under-time/absence/unpaid-leave cuts against it, then holiday and rest-day lines
// (premium vs full pay per the employee's inclusion settings), overtime/night differential and
// other income. Employee info, Deductions, Net Pay and signature are the shared PayslipSections.
// Only used for Regular payrolls -- see PayslipDocumentFactory.
public class PayslipFixedDocument : IDocument
{
    private readonly Payroll _p;
    private readonly EmployeeFullModel _e;
    private readonly Company? _company;
    private readonly PayslipOtherIncome _otherIncome;

    public PayslipFixedDocument(Payroll payroll, EmployeeFullModel employee, Company? company = null,
        PayslipOtherIncome? otherIncome = null)
    {
        _p = payroll;
        _e = employee;
        _company = company;
        _otherIncome = otherIncome ?? PayslipOtherIncome.Categories;
    }

    public DocumentMetadata GetMetadata() => new DocumentMetadata
    {
        Title = $"Payslip - {_e.FullName} - {_p.PayPeriodStart:MMM dd} to {_p.PayPeriodEnd:MMM dd, yyyy}",
        Author = PayslipSections.CompanyName(_company),
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

            page.Header().Element(c => PayslipSections.ComposePageHeader(c, _p, _e, _company));
            page.Content().PaddingTop(8).Element(ComposeContent);
            page.Footer().Element(ReportHeaderComposer.ComposeFooter);
        });
    }

    void ComposeContent(IContainer c)
    {
        c.Column(col =>
        {
            col.Spacing(8);
            col.Item().Element(ComposeEmployeeInfo);
            col.Item().Row(row =>
            {
                row.Spacing(8);
                row.RelativeItem().Element(ComposeEarnings);
                row.RelativeItem().Element(c2 => PayslipSections.ComposeDeductions(c2, _p));
            });
            col.Item().Element(c2 => PayslipSections.ComposeNetPay(c2, _p));
            col.Item().Element(c2 => PayslipSections.ComposeReceivedBy(c2, _p, _e));
        });
    }

    // Same block as PayslipSections.ComposeEmployeeInfo, except "Days Worked" (counted from DTR
    // pay rows, which a Fixed employee's regular days don't produce) is replaced by the period's
    // Basic Salary.
    void ComposeEmployeeInfo(IContainer c)
    {
        var basic = new FixedPayslipRowBuilder(_p).BasicSalary;
        c.Border(1).BorderColor(ReportDocumentStyle.BorderColor).Column(col =>
        {
            col.Item().Element(c2 => PayslipSections.SectionHeader(c2, "EMPLOYEE INFORMATION"));
            PayslipSections.TwoColRow(col,
                ("Position", _e.PositionName),
                ("Department", _e.DepartmentName));
            PayslipSections.TwoColRow(col,
                ("Client / Site", _e.ClientName),
                ("Payroll Group", _e.PayrollGroupName));
            PayslipSections.TwoColRow(col,
                ("Salary Type", _p.SalaryType.ToString()),
                ("Daily Rate", _p.DailyRate > 0 ? PayslipSections.Money(_p.DailyRate) : "—"));
            PayslipSections.TwoColRow(col,
                ("Payroll Date", _p.PayrollDate.ToString("MMM dd, yyyy")),
                ("Pay Date", _p.PayDate?.ToString("MMM dd, yyyy") ?? "—"));
            PayslipSections.TwoColRow(col,
                ("Basic Salary", basic > 0 ? PayslipSections.Money(basic) : "—"),
                ("Period", $"{_p.PayPeriodStart:MMM dd} – {_p.PayPeriodEnd:MMM dd, yyyy}"));
        });
    }

    void ComposeEarnings(IContainer c)
    {
        c.Border(1).BorderColor(ReportDocumentStyle.BorderColor).Column(col =>
        {
            col.Item().Element(c2 => PayslipSections.SectionHeader(c2, "EARNINGS"));
            var rows = new FixedPayslipRowBuilder(_p, _otherIncome).Build();
            col.Item().Padding(4).Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(3);
                    cols.RelativeColumn(1);
                });

                foreach (var row in rows)
                {
                    switch (row.Kind)
                    {
                        case FixedPayslipRowKind.Header:
                            PayslipSections.SubHeaderRow(table, row.Label);
                            break;
                        case FixedPayslipRowKind.Subtotal:
                            PayslipSections.AmountRow(table, row.Label, row.Amount, bold: true);
                            break;
                        case FixedPayslipRowKind.Cut:
                            // Taken off Basic Salary, indented under it, shown as a negative.
                            table.Cell().PaddingVertical(3).PaddingLeft(14).Text($"Less: {row.Label}");
                            table.Cell().PaddingVertical(3).AlignRight().Text(PayslipSections.Money(-row.Amount));
                            break;
                        case FixedPayslipRowKind.Note:
                            table.Cell().PaddingVertical(2).PaddingLeft(14).Text(row.Label).FontSize(7.5f).Italic().FontColor(ReportDocumentStyle.LabelColor);
                            table.Cell().PaddingVertical(2).AlignRight().Text(PayslipSections.Money(row.Amount)).FontSize(7.5f).Italic().FontColor(ReportDocumentStyle.LabelColor);
                            break;
                        default:
                            PayslipSections.AmountRow(table, row.Label, row.Amount);
                            break;
                    }
                }

                PayslipSections.AmountRow(table, "GROSS INCOME", _p.GrossIncome, bold: true);
            });

            var hasLeaveNote = !string.IsNullOrWhiteSpace(_p.PaidLeaveBreakdown)
                || !string.IsNullOrWhiteSpace(_p.OneTimePayoutBreakdown)
                || _p.NonCompanyPaidLeaves > 0;
            if (!hasLeaveNote) return;
            col.Item().PaddingHorizontal(4).PaddingBottom(4).Column(detail =>
            {
                if (!string.IsNullOrWhiteSpace(_p.PaidLeaveBreakdown))
                    detail.Item().Text($"Leave detail: {_p.PaidLeaveBreakdown}").FontSize(7).Italic().FontColor(ReportDocumentStyle.LabelColor);
                if (_p.NonCompanyPaidLeaves > 0)
                    // Informational only -- same note PayslipDocument prints; not part of Gross Income.
                    detail.Item().Text($"Includes {PayslipSections.Money(_p.NonCompanyPaidLeaves)} in Government/Shared-funded paid leave (informational)").FontSize(7).Italic().FontColor(ReportDocumentStyle.LabelColor);
                if (!string.IsNullOrWhiteSpace(_p.OneTimePayoutBreakdown))
                    detail.Item().Text($"One-Time Leave Payout detail: {_p.OneTimePayoutBreakdown}").FontSize(7).Italic().FontColor(ReportDocumentStyle.LabelColor);
            });
        });
    }
}
