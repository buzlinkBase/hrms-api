using System.Globalization;
using Hrms.Domain.Entities;
using Hrms.Domain.ValueObjects;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Hrms.Api.Documents.Shared;

// Sections shared by every payslip layout — pulled out of PayslipDocument.cs so a second format
// (PayslipHoursDocument, the "hours x rate" layout) doesn't duplicate the employee info/
// deductions/net pay/signature blocks, which are identical regardless of how Earnings is
// presented. Only ComposeEarnings differs between formats, so it stays document-specific.
internal static class PayslipSections
{
    // Falls back to the product name until the tenant fills in Company Setup.
    public static string CompanyName(Company? company) => ReportDocumentStyle.CompanyName(company);

    public static string Money(decimal v) => ReportDocumentStyle.Money(v);

    public static void ComposePageHeader(IContainer c, Payroll p, EmployeeFullModel e, Company? company) =>
        c.BorderBottom(1).BorderColor(ReportDocumentStyle.Primary).PaddingBottom(6).Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text(CompanyName(company)).Bold().FontSize(12).FontColor(ReportDocumentStyle.Primary);
                col.Item().Text(p.PayrollType switch
                {
                    PayrollType.ThirteenthMonth => "13TH MONTH PAY",
                    PayrollType.LastPay => "LAST PAY",
                    PayrollType.YearEndAdjustment => "YEAR-END TAX ADJUSTMENT",
                    _ => "PAYSLIP",
                }).FontSize(9).FontColor(ReportDocumentStyle.LabelColor);
                if (!string.IsNullOrWhiteSpace(company?.Address) || !string.IsNullOrWhiteSpace(company?.Contact))
                {
                    col.Item().Text(string.Join("  •  ", new[] { company?.Address, company?.Contact }
                        .Where(s => !string.IsNullOrWhiteSpace(s))))
                        .FontSize(7.5f).FontColor(ReportDocumentStyle.LabelColor);
                }
            });
            row.ConstantItem(200).AlignRight().Column(col =>
            {
                col.Item().Text(e.FullName ?? "—").Bold().FontSize(11);
                col.Item().Text($"#{e.EmployeeNo}").FontSize(9).FontColor(ReportDocumentStyle.LabelColor);
                col.Item().Text(
                    $"{p.PayPeriodStart:MMM dd} – {p.PayPeriodEnd:MMM dd, yyyy}")
                    .FontSize(9).FontColor(ReportDocumentStyle.Primary);
            });
        });

    public static void SectionHeader(IContainer c, string title) =>
        c.Background(ReportDocumentStyle.SectionHeaderBg)
         .BorderBottom(1).BorderColor(ReportDocumentStyle.BorderColor)
         .Padding(5)
         .Text(title).Bold().FontSize(9).FontColor(ReportDocumentStyle.Primary);

    public static void LabelValue(IContainer c, string label, string? value) =>
        c.Padding(3).Row(row =>
        {
            row.ConstantItem(110).Text(label).FontColor(ReportDocumentStyle.LabelColor);
            row.RelativeItem().Text(value ?? "—");
        });

    public static void TwoColRow(ColumnDescriptor col, (string label, string? value) left, (string label, string? value) right) =>
        col.Item().Row(row =>
        {
            row.RelativeItem().Element(c => LabelValue(c, left.label, left.value));
            row.RelativeItem().Element(c => LabelValue(c, right.label, right.value));
        });

    // Amount row helper for the earnings/deductions tables — a plain line item, a
    // sub-section header (spans both columns), or a bold ruled-off total.
    public static void AmountRow(TableDescriptor table, string label, decimal amount, bool bold = false)
    {
        var labelCell = table.Cell().Padding(3);
        var valueCell = table.Cell().Padding(3).AlignRight();
        if (bold)
        {
            labelCell = labelCell.BorderTop(1).BorderColor(ReportDocumentStyle.BorderColor);
            valueCell = valueCell.BorderTop(1).BorderColor(ReportDocumentStyle.BorderColor);
        }
        var labelText = labelCell.Text(label);
        var valueText = valueCell.Text(Money(amount));
        if (bold)
        {
            labelText.Bold();
            valueText.Bold();
        }
    }

    public static void SubHeaderRow(TableDescriptor table, string title) =>
        table.Cell().ColumnSpan(2).Background(ReportDocumentStyle.TableHeaderBg).Padding(3).Text(title).Bold().FontSize(8);

    public static void ComposeEmployeeInfo(IContainer c, Payroll p, EmployeeFullModel e)
    {
        c.Border(1).BorderColor(ReportDocumentStyle.BorderColor).Column(col =>
        {
            col.Item().Element(c2 => SectionHeader(c2, "EMPLOYEE INFORMATION"));
            TwoColRow(col,
                ("Position", e.PositionName),
                ("Department", e.DepartmentName));
            TwoColRow(col,
                ("Client / Site", e.ClientName),
                ("Payroll Group", e.PayrollGroupName));
            TwoColRow(col,
                ("Salary Type", p.SalaryType.ToString()),
                ("Daily Rate", p.DailyRate > 0 ? Money(p.DailyRate) : "—"));
            TwoColRow(col,
                ("Payroll Date", p.PayrollDate.ToString("MMM dd, yyyy")),
                ("Pay Date", p.PayDate?.ToString("MMM dd, yyyy") ?? "—"));
            TwoColRow(col,
                ("Week #", ISOWeek.GetWeekOfYear(p.PayPeriodStart.ToDateTime(TimeOnly.MinValue)).ToString()),
                ("Days Worked", $"{DaysWorked(p)} days"));
        });
    }

    // Distinct DTR dates with any pay > 0 across every day-type/tier — a day only ever has
    // exactly one of the 8 day-type categories' 4 tiers (Day/OT/ND/NDOT) populated (its
    // WorkType for that date), so summing across all of them is equivalent to switching on
    // WorkType, without needing to. Read from Payroll.TimeHourPayResults, the per-day DTR
    // breakdown already saved with this payroll row (lazy-loaded — UseLazyLoadingProxies).
    // Internal (not private), matching this codebase's testability convention, so hrms.test can
    // verify the count directly without rendering a PDF.
    internal static int DaysWorked(Payroll p) => p.TimeHourPayResults
        .Where(d =>
            d.RegularDayPay + d.RegularOTPay + d.RegularNDPay + d.RegularNDOTPay
            + d.RestDayPay + d.RestDayOTPay + d.RestDayNDPay + d.RestDayNDOTPay
            + d.LegalPay + d.LegalOTPay + d.LegalNDPay + d.LegalNDOTPay
            + d.SpecialPay + d.SpecialOTPay + d.SpecialNDPay + d.SpecialNDOTPay
            + d.RestLegalPay + d.RestLegalOTPay + d.RestLegalNDPay + d.RestLegalNDOTPay
            + d.RestSpecialPay + d.RestSpecialOTPay + d.RestSpecialNDPay + d.RestSpecialNDOTPay
            + d.DoubleLegalPay + d.DoubleLegalOTPay + d.DoubleLegalNDPay + d.DoubleLegalNDOTPay
            + d.RestDoubleLegalPay + d.RestDoubleLegalOTPay + d.RestDoubleLegalNDPay + d.RestDoubleLegalNDOTPay
            > 0)
        .Select(d => d.Date)
        .Distinct()
        .Count();

    // Distinct DTR dates with any pay from one of the 6 holiday-involving day types (Legal,
    // Special, Rest+Legal, Rest+Special, Double Legal, Rest+Double Legal) — used by
    // PayslipHoursDocument's "Holiday/s" group heading. RegularDaysWorked is just the complement
    // against DaysWorked, since a day only ever has exactly one of the 8 day-type categories
    // populated (see DaysWorked's own comment) — Regular and Rest Day both count as "regular"
    // here, since neither is a paid holiday.
    internal static int HolidayDaysWorked(Payroll p) => p.TimeHourPayResults
        .Where(d =>
            d.LegalPay + d.LegalOTPay + d.LegalNDPay + d.LegalNDOTPay
            + d.SpecialPay + d.SpecialOTPay + d.SpecialNDPay + d.SpecialNDOTPay
            + d.RestLegalPay + d.RestLegalOTPay + d.RestLegalNDPay + d.RestLegalNDOTPay
            + d.RestSpecialPay + d.RestSpecialOTPay + d.RestSpecialNDPay + d.RestSpecialNDOTPay
            + d.DoubleLegalPay + d.DoubleLegalOTPay + d.DoubleLegalNDPay + d.DoubleLegalNDOTPay
            + d.RestDoubleLegalPay + d.RestDoubleLegalOTPay + d.RestDoubleLegalNDPay + d.RestDoubleLegalNDOTPay
            > 0)
        .Select(d => d.Date)
        .Distinct()
        .Count();

    internal static int RegularDaysWorked(Payroll p) => DaysWorked(p) - HolidayDaysWorked(p);

    public static void ComposeDeductions(IContainer c, Payroll p)
    {
        c.Border(1).BorderColor(ReportDocumentStyle.BorderColor).Column(col =>
        {
            col.Item().Element(c2 => SectionHeader(c2, "DEDUCTIONS"));
            col.Item().Padding(4).Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(3);
                    cols.RelativeColumn(1);
                });

                var isThirteenthMonth = p.PayrollType == PayrollType.ThirteenthMonth;
                var isLastPay = p.PayrollType == PayrollType.LastPay;
                var isYearEndAdjustment = p.PayrollType == PayrollType.YearEndAdjustment;
                var isRegular = !isThirteenthMonth && !isLastPay && !isYearEndAdjustment;
                if (isRegular)
                {
                    AmountRow(table, "SSS Contribution", p.SSSContribution);
                    AmountRow(table, "PhilHealth Contribution", p.PhilHealthContribution);
                    AmountRow(table, "Pag-IBIG Contribution", p.PagIbigContribution);
                }
                AmountRow(table, "Withholding Tax", p.WithholdingTax);
                if (isLastPay)
                {
                    // Netted straight off Net Pay by GenerateLastPayAsync — informational
                    // only, the loan ledger itself is untouched by this payout.
                    AmountRow(table, "Outstanding Loans", p.TotalLoans);
                }
                if (isRegular)
                {
                    AmountRow(table, "Loans", p.TotalLoans);
                    AmountRow(table, "Other Deductions", p.OtherDeductions - p.TotalLoans);
                    if (p.SalaryType == SalaryType.FIXED)
                    {
                        AmountRow(table, "Late", p.LateAmount);
                        AmountRow(table, "Under Time", p.UnderTimeAmount);
                        AmountRow(table, "Absences", p.AbsencesAmount);
                    }
                }
                AmountRow(table, "TOTAL DEDUCTIONS", p.TotalDeductions, bold: true);
            });
        });
    }

    public static void ComposeNetPay(IContainer c, Payroll p)
    {
        c.Column(col =>
        {
            // Government-funded one-time leave payout — a non-taxable benefit pass-through
            // deliberately excluded from Gross Income/statutory bases, so it's added to Net
            // Pay here rather than shown inside the taxable Earnings box above.
            if (p.GovernmentFundedLeavePay > 0)
            {
                col.Item().PaddingBottom(4).Row(row =>
                {
                    row.RelativeItem().Text("One-Time Leave Payout (Government, Non-Taxable)").FontColor(ReportDocumentStyle.LabelColor);
                    row.ConstantItem(150).AlignRight().Text(Money(p.GovernmentFundedLeavePay)).FontColor(ReportDocumentStyle.LabelColor);
                });
            }
            col.Item().Background(ReportDocumentStyle.SectionHeaderBg).Border(1).BorderColor(ReportDocumentStyle.Primary).Padding(10).Row(row =>
            {
                row.RelativeItem().Text("NET PAY").Bold().FontSize(12).FontColor(ReportDocumentStyle.Primary);
                row.ConstantItem(150).AlignRight().Text(Money(p.NetPay)).Bold().FontSize(14).FontColor(ReportDocumentStyle.Primary);
            });
        });
    }

    // Acknowledgment of receipt — a blank signature line for a printed copy, or (once the
    // employee has confirmed receipt in the Employee Portal) a note showing when instead, so a
    // re-print after digital acknowledgment doesn't ask for a redundant physical signature. See
    // Payroll.AcknowledgedAt / MeController.AcknowledgeMyPayslip.
    public static void ComposeReceivedBy(IContainer c, Payroll p, EmployeeFullModel e)
    {
        if (p.AcknowledgedAt.HasValue)
        {
            c.PaddingTop(16).Text(
                $"Digitally acknowledged by {e.FullName} via Employee Portal on {p.AcknowledgedAt.Value:MMM dd, yyyy hh:mm tt}.")
                .FontSize(8).Italic().FontColor(ReportDocumentStyle.LabelColor);
            return;
        }

        c.PaddingTop(20).Row(row =>
        {
            row.RelativeItem().Column(sig =>
            {
                sig.Item().PaddingTop(30).BorderTop(1).BorderColor(ReportDocumentStyle.TextColor).PaddingTop(2)
                    .Text(e.FullName ?? "—").Bold();
                sig.Item().Text("Received by (Employee Signature)").FontSize(8).FontColor(ReportDocumentStyle.LabelColor);
            });
            row.ConstantItem(24);
            row.ConstantItem(140).Column(sig =>
            {
                sig.Item().PaddingTop(30).BorderTop(1).BorderColor(ReportDocumentStyle.TextColor);
                sig.Item().Text("Date").FontSize(8).FontColor(ReportDocumentStyle.LabelColor);
            });
        });
    }
}
