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
    private readonly Company? _company;

    private static readonly string Primary = "#1DA081";
    private static readonly string SectionHeaderBg = "#f5f5f5";
    private static readonly string SubHeaderBg = "#e8f5f1";
    private static readonly string BorderColor = "#d9d9d9";
    private static readonly string LabelColor = "#666666";
    private static readonly string TextColor = "#1a1a1a";

    // Falls back to the product name until the tenant fills in Company Setup.
    private string CompanyName => string.IsNullOrWhiteSpace(_company?.Description) ? "One Punch HRIS" : _company.Description;

    public PayslipDocument(Payroll payroll, EmployeeFullModel employee, Company? company = null)
    {
        _p = payroll;
        _e = employee;
        _company = company;
    }

    public DocumentMetadata GetMetadata() => new DocumentMetadata
    {
        Title = $"Payslip - {_e.FullName} - {_p.PayPeriodStart:MMM dd} to {_p.PayPeriodEnd:MMM dd, yyyy}",
        Author = CompanyName,
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
                col.Item().Text(CompanyName).Bold().FontSize(12).FontColor(Primary);
                col.Item().Text(_p.PayrollType switch
                {
                    PayrollType.ThirteenthMonth => "13TH MONTH PAY",
                    PayrollType.LastPay => "LAST PAY",
                    _ => "PAYSLIP",
                }).FontSize(9).FontColor(LabelColor);
                if (!string.IsNullOrWhiteSpace(_company?.Address) || !string.IsNullOrWhiteSpace(_company?.Contact))
                {
                    col.Item().Text(string.Join("  •  ", new[] { _company?.Address, _company?.Contact }
                        .Where(s => !string.IsNullOrWhiteSpace(s))))
                        .FontSize(7.5f).FontColor(LabelColor);
                }
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
            col.Item().Row(row =>
            {
                row.Spacing(8);
                row.RelativeItem().Element(ComposeEarnings);
                row.RelativeItem().Element(ComposeDeductions);
            });
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
        if (_p.PayrollType == PayrollType.ThirteenthMonth)
        {
            ComposeThirteenthMonthEarnings(c);
            return;
        }
        if (_p.PayrollType == PayrollType.LastPay)
        {
            ComposeLastPayEarnings(c);
            return;
        }
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

                // FIXED employees' Basic Pay (MonthlyRate/divisor) already pays for every day
                // in the period, paid-leave days folded in — shown here net of PaidLeaves so
                // the Basic Pay and Paid Leave lines don't double-count the same money.
                // VARIABLE's Basic Pay (RegularDayPay only) never included paid-leave pay, so
                // it's shown as-is. See PayrollProcessorService.GetBasicPay/ComputeBasicSalary.
                AmountRow(table, "Basic Pay",
                    _p.SalaryType == SalaryType.FIXED ? _p.BasicPay - _p.PaidLeaves : _p.BasicPay);
                AmountRow(table, "Regular Overtime", _p.RegularOTPay);
                AmountRow(table, "Regular Night Differential", _p.RegularNDPay);
                AmountRow(table, "Regular Night Diff. Overtime", _p.RegularNDOTPay);
                AmountRow(table, "Rest Day",
                    _p.RestDayPay + _p.RestDayOTPay + _p.RestDayNDPay + _p.RestDayNDOTPay);
                AmountRow(table, "Paid Leave", _p.PaidLeaves);
                // Unpaid Leave is shown as-is regardless of salary type — FIXED's Basic Pay
                // above is already computed net of it (GetBasicPay subtracts it from the flat
                // monthly rate), and VARIABLE's day-by-day calc simply never generates pay for
                // an unpaid-leave day, so in both cases it was never part of Basic Pay to
                // begin with and needs no further adjustment here.
                AmountRow(table, "Unpaid Leave", _p.UnpaidLeaves);
                if (_p.CompanyFundedLeavePay > 0)
                {
                    // Government-funded is deliberately NOT shown here — it's a non-taxable
                    // pass-through excluded from Gross Income, shown near Net Pay instead
                    // (see ComposeNetPay). Only the taxable Company-funded slice belongs in
                    // Earnings — see PayrollProcessorService.ApplyOneTimeLeavePayoutsToGross.
                    AmountRow(table, "One-Time Leave Payout (Company)", _p.CompanyFundedLeavePay);
                }
                SubHeaderRow(table, "HOLIDAY BREAKDOWN");
                AmountRow(table, "Legal Holiday (Unworked)", _p.LegalHolidayUnworkedPay);
                AmountRow(table, "Legal Holiday Duty (Worked)",
                    (_p.LegalPay - _p.LegalHolidayUnworkedPay) + _p.LegalOTPay + _p.LegalNDPay + _p.LegalNDOTPay);
                AmountRow(table, "Rest Day + Legal Holiday",
                    _p.RestLegalPay + _p.RestLegalOTPay + _p.RestLegalNDPay + _p.RestLegalNDOTPay);
                AmountRow(table, "Special Holiday",
                    _p.SpecialPay + _p.SpecialOTPay + _p.SpecialNDPay + _p.SpecialNDOTPay);
                AmountRow(table, "Rest Day + Special Holiday",
                    _p.RestSpecialPay + _p.RestSpecialOTPay + _p.RestSpecialNDPay + _p.RestSpecialNDOTPay);
                AmountRow(table, "Double Legal Holiday",
                    _p.DoubleLegalPay + _p.DoubleLegalOTPay + _p.DoubleLegalNDPay + _p.DoubleLegalNDOTPay);
                AmountRow(table, "Rest Day + Double Legal Holiday",
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
            var hasLeaveNote = !string.IsNullOrWhiteSpace(_p.PaidLeaveBreakdown)
                || !string.IsNullOrWhiteSpace(_p.OneTimePayoutBreakdown)
                || _p.NonCompanyPaidLeaves > 0;
            if (hasLeaveNote)
            {
                col.Item().PaddingHorizontal(4).PaddingBottom(4).Column(detail =>
                {
                    if (!string.IsNullOrWhiteSpace(_p.PaidLeaveBreakdown))
                        detail.Item().Text($"Paid Leave detail: {_p.PaidLeaveBreakdown}").FontSize(7).Italic().FontColor(LabelColor);
                    if (_p.NonCompanyPaidLeaves > 0)
                        // Informational only — NOT included in Gross Income above. Regular
                        // (non-one-time) Government/Shared-funded paid leave, e.g. an SSS
                        // maternity day, out of the Paid Leave total. FIXED's Basic Pay only
                        // ever covers the Company-funded share of paid leave.
                        detail.Item().Text($"Includes {Money(_p.NonCompanyPaidLeaves)} in Government/Shared-funded paid leave (informational)").FontSize(7).Italic().FontColor(LabelColor);
                    if (!string.IsNullOrWhiteSpace(_p.OneTimePayoutBreakdown))
                        detail.Item().Text($"One-Time Leave Payout detail: {_p.OneTimePayoutBreakdown}").FontSize(7).Italic().FontColor(LabelColor);
                });
            }
        });
    }

    // 13th Month Pay (PD 851) is a lump-sum annual payout, not attendance-driven — none of
    // the OT/ND/holiday/other-income breakdown or SSS/PhilHealth/Pag-IBIG rows apply (see
    // PayrollProcessorService.GenerateThirteenthMonthAsync, which never invokes those
    // calculators for this PayrollType). Shows the non-taxable/taxable ceiling split instead,
    // since that — not a DTR breakdown — is the figure that explains this payslip's tax.
    void ComposeThirteenthMonthEarnings(IContainer c)
    {
        c.Border(1).BorderColor(BorderColor).Column(col =>
        {
            col.Item().Element(c2 => SectionHeader(c2, "13TH MONTH PAY"));
            col.Item().Padding(4).Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(3);
                    cols.RelativeColumn(1);
                });

                AmountRow(table, "13th Month Pay", _p.GrossIncome, bold: true);
                SubHeaderRow(table, "TAX TREATMENT");
                AmountRow(table, "Non-Taxable Portion", _p.NonTaxableBenefits);
                AmountRow(table, "Taxable Portion", _p.TaxableBenefits);
            });
        });
    }

    // Last Pay / Final Pay (DOLE Labor Advisory 06-20): prorated 13th month + leave credit
    // cash conversion, combined into one GrossIncome figure by GenerateLastPayAsync (the two
    // components aren't persisted separately) — same non-taxable/taxable ceiling split shown
    // for 13th Month Pay, since it's the figure that explains this payslip's tax. Final DTR-
    // attendance wages for days actually worked aren't part of this document; they're on a
    // separate regular payslip from whatever payroll run covered those days.
    void ComposeLastPayEarnings(IContainer c)
    {
        c.Border(1).BorderColor(BorderColor).Column(col =>
        {
            col.Item().Element(c2 => SectionHeader(c2, "LAST PAY"));
            col.Item().Padding(4).Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(3);
                    cols.RelativeColumn(1);
                });

                AmountRow(table, "Prorated 13th Month + Leave Conversion", _p.GrossIncome, bold: true);
                SubHeaderRow(table, "TAX TREATMENT");
                AmountRow(table, "Non-Taxable Portion", _p.NonTaxableBenefits);
                AmountRow(table, "Taxable Portion", _p.TaxableBenefits);
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

                var isThirteenthMonth = _p.PayrollType == PayrollType.ThirteenthMonth;
                var isLastPay = _p.PayrollType == PayrollType.LastPay;
                var isRegular = !isThirteenthMonth && !isLastPay;
                if (isRegular)
                {
                    AmountRow(table, "SSS Contribution", _p.SSSContribution);
                    AmountRow(table, "PhilHealth Contribution", _p.PhilHealthContribution);
                    AmountRow(table, "Pag-IBIG Contribution", _p.PagIbigContribution);
                }
                AmountRow(table, "Withholding Tax", _p.WithholdingTax);
                if (isLastPay)
                {
                    // Netted straight off Net Pay by GenerateLastPayAsync — informational
                    // only, the loan ledger itself is untouched by this payout.
                    AmountRow(table, "Outstanding Loans", _p.TotalLoans);
                }
                if (isRegular)
                {
                    AmountRow(table, "Loans", _p.TotalLoans);
                    AmountRow(table, "Other Deductions", _p.OtherDeductions - _p.TotalLoans);
                    if (_p.SalaryType == SalaryType.FIXED)
                    {
                        AmountRow(table, "Late", _p.LateAmount);
                        AmountRow(table, "Under Time", _p.UnderTimeAmount);
                        AmountRow(table, "Absences", _p.AbsencesAmount);
                    }
                }
                AmountRow(table, "TOTAL DEDUCTIONS", _p.TotalDeductions, bold: true);
            });
        });
    }

    void ComposeNetPay(IContainer c)
    {
        c.Column(col =>
        {
            // Government-funded one-time leave payout — a non-taxable benefit pass-through
            // deliberately excluded from Gross Income/statutory bases (see
            // PayrollProcessorService.ApplyOneTimeLeavePayoutsToGross), so it's added to Net
            // Pay here rather than shown inside the taxable Earnings box above.
            if (_p.GovernmentFundedLeavePay > 0)
            {
                col.Item().PaddingBottom(4).Row(row =>
                {
                    row.RelativeItem().Text("One-Time Leave Payout (Government, Non-Taxable)").FontColor(LabelColor);
                    row.ConstantItem(150).AlignRight().Text(Money(_p.GovernmentFundedLeavePay)).FontColor(LabelColor);
                });
            }
            col.Item().Background(SubHeaderBg).Border(1).BorderColor(Primary).Padding(10).Row(row =>
            {
                row.RelativeItem().Text("NET PAY").Bold().FontSize(12).FontColor(Primary);
                row.ConstantItem(150).AlignRight().Text(Money(_p.NetPay)).Bold().FontSize(14).FontColor(Primary);
            });
        });
    }
}
