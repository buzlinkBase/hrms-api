using Hrms.Api.Documents.Shared;
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

    public PayslipDocument(Payroll payroll, EmployeeFullModel employee, Company? company = null)
    {
        _p = payroll;
        _e = employee;
        _company = company;
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
            col.Item().Element(c2 => PayslipSections.ComposeEmployeeInfo(c2, _p, _e));
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

    // ── Earnings (the only section that differs by PayrollType) ────────────────

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
        if (_p.PayrollType == PayrollType.YearEndAdjustment)
        {
            ComposeYearEndAdjustmentEarnings(c);
            return;
        }
        c.Border(1).BorderColor(ReportDocumentStyle.BorderColor).Column(col =>
        {
            col.Item().Element(c2 => PayslipSections.SectionHeader(c2, "EARNINGS"));
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
                PayslipSections.AmountRow(table, "Basic Pay",
                    _p.SalaryType == SalaryType.FIXED ? _p.BasicPay - _p.PaidLeaves : _p.BasicPay);
                PayslipSections.AmountRow(table, "Regular Overtime", _p.RegularOTPay);
                PayslipSections.AmountRow(table, "Regular Night Differential", _p.RegularNDPay);
                PayslipSections.AmountRow(table, "Regular Night Diff. Overtime", _p.RegularNDOTPay);
                PayslipSections.AmountRow(table, "Rest Day",
                    _p.RestDayPay + _p.RestDayOTPay + _p.RestDayNDPay + _p.RestDayNDOTPay);
                PayslipSections.AmountRow(table, "Paid Leave", _p.PaidLeaves);
                // Unpaid Leave is shown as-is regardless of salary type — FIXED's Basic Pay
                // above is already computed net of it (GetBasicPay subtracts it from the flat
                // monthly rate), and VARIABLE's day-by-day calc simply never generates pay for
                // an unpaid-leave day, so in both cases it was never part of Basic Pay to
                // begin with and needs no further adjustment here.
                PayslipSections.AmountRow(table, "Unpaid Leave", _p.UnpaidLeaves);
                if (_p.CompanyFundedLeavePay > 0)
                {
                    // Government-funded is deliberately NOT shown here — it's a non-taxable
                    // pass-through excluded from Gross Income, shown near Net Pay instead
                    // (see PayslipSections.ComposeNetPay). Only the taxable Company-funded
                    // slice belongs in Earnings — see
                    // PayrollProcessorService.ApplyOneTimeLeavePayoutsToGross.
                    PayslipSections.AmountRow(table, "One-Time Leave Payout (Company)", _p.CompanyFundedLeavePay);
                }
                PayslipSections.SubHeaderRow(table, "HOLIDAY BREAKDOWN");
                PayslipSections.AmountRow(table, "Legal Holiday (Unworked)", _p.LegalHolidayUnworkedPay);
                PayslipSections.AmountRow(table, "Legal Holiday Duty (Worked)",
                    (_p.LegalPay - _p.LegalHolidayUnworkedPay) + _p.LegalOTPay + _p.LegalNDPay + _p.LegalNDOTPay);
                PayslipSections.AmountRow(table, "Rest Day + Legal Holiday",
                    _p.RestLegalPay + _p.RestLegalOTPay + _p.RestLegalNDPay + _p.RestLegalNDOTPay);
                PayslipSections.AmountRow(table, "Special Holiday",
                    _p.SpecialPay + _p.SpecialOTPay + _p.SpecialNDPay + _p.SpecialNDOTPay);
                PayslipSections.AmountRow(table, "Rest Day + Special Holiday",
                    _p.RestSpecialPay + _p.RestSpecialOTPay + _p.RestSpecialNDPay + _p.RestSpecialNDOTPay);
                PayslipSections.AmountRow(table, "Double Legal Holiday",
                    _p.DoubleLegalPay + _p.DoubleLegalOTPay + _p.DoubleLegalNDPay + _p.DoubleLegalNDOTPay);
                PayslipSections.AmountRow(table, "Rest Day + Double Legal Holiday",
                    _p.RestDoubleLegalPay + _p.RestDoubleLegalOTPay + _p.RestDoubleLegalNDPay + _p.RestDoubleLegalNDOTPay);

                PayslipSections.SubHeaderRow(table, "OTHER INCOME");
                PayslipSections.AmountRow(table, "COLA", _p.Cola);
                PayslipSections.AmountRow(table, "Regular Allowances", _p.TotalRegularAllowances);
                PayslipSections.AmountRow(table, "Bonuses", _p.TotalBonuses);
                PayslipSections.AmountRow(table, "Commissions", _p.TotalCommissions);
                PayslipSections.AmountRow(table, "De Minimis", _p.TotalDeminimises);
                PayslipSections.AmountRow(table, "Reimbursement", _p.Reimbursement);
                PayslipSections.AmountRow(table, "Other Income", _p.TotalOtherIncome);

                PayslipSections.AmountRow(table, "GROSS INCOME", _p.GrossIncome, bold: true);
            });
            var hasLeaveNote = !string.IsNullOrWhiteSpace(_p.PaidLeaveBreakdown)
                || !string.IsNullOrWhiteSpace(_p.OneTimePayoutBreakdown)
                || _p.NonCompanyPaidLeaves > 0;
            if (hasLeaveNote)
            {
                col.Item().PaddingHorizontal(4).PaddingBottom(4).Column(detail =>
                {
                    if (!string.IsNullOrWhiteSpace(_p.PaidLeaveBreakdown))
                        detail.Item().Text($"Paid Leave detail: {_p.PaidLeaveBreakdown}").FontSize(7).Italic().FontColor(ReportDocumentStyle.LabelColor);
                    if (_p.NonCompanyPaidLeaves > 0)
                        // Informational only — NOT included in Gross Income above. Regular
                        // (non-one-time) Government/Shared-funded paid leave, e.g. an SSS
                        // maternity day, out of the Paid Leave total. FIXED's Basic Pay only
                        // ever covers the Company-funded share of paid leave.
                        detail.Item().Text($"Includes {PayslipSections.Money(_p.NonCompanyPaidLeaves)} in Government/Shared-funded paid leave (informational)").FontSize(7).Italic().FontColor(ReportDocumentStyle.LabelColor);
                    if (!string.IsNullOrWhiteSpace(_p.OneTimePayoutBreakdown))
                        detail.Item().Text($"One-Time Leave Payout detail: {_p.OneTimePayoutBreakdown}").FontSize(7).Italic().FontColor(ReportDocumentStyle.LabelColor);
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
        c.Border(1).BorderColor(ReportDocumentStyle.BorderColor).Column(col =>
        {
            col.Item().Element(c2 => PayslipSections.SectionHeader(c2, "13TH MONTH PAY"));
            col.Item().Padding(4).Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(3);
                    cols.RelativeColumn(1);
                });

                PayslipSections.AmountRow(table, "13th Month Pay", _p.GrossIncome, bold: true);
                PayslipSections.SubHeaderRow(table, "TAX TREATMENT");
                PayslipSections.AmountRow(table, "Non-Taxable Portion", _p.NonTaxableBenefits);
                PayslipSections.AmountRow(table, "Taxable Portion", _p.TaxableBenefits);
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
        c.Border(1).BorderColor(ReportDocumentStyle.BorderColor).Column(col =>
        {
            col.Item().Element(c2 => PayslipSections.SectionHeader(c2, "LAST PAY"));
            col.Item().Padding(4).Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(3);
                    cols.RelativeColumn(1);
                });

                PayslipSections.AmountRow(table, "Prorated 13th Month + Leave Conversion", _p.GrossIncome, bold: true);
                PayslipSections.SubHeaderRow(table, "TAX TREATMENT");
                PayslipSections.AmountRow(table, "Non-Taxable Portion", _p.NonTaxableBenefits);
                PayslipSections.AmountRow(table, "Taxable Portion", _p.TaxableBenefits);
            });
        });
    }

    // Year-End Tax Annualization (RR 11-2018 §2.79.4): the row's WithholdingTax is the signed
    // adjustment amount computed by TaxAnnualizationService (positive = additional tax
    // collected, negative = refund) — GrossIncome is always 0 for this PayrollType, so there's
    // no DTR/earnings breakdown to show, only the result. The full annual computation trail
    // (annual gross/taxable income/tax due/withheld YTD) isn't persisted on the row itself, so
    // it can't be reprinted here — only the final adjustment amount and its sign.
    void ComposeYearEndAdjustmentEarnings(IContainer c)
    {
        var isRefund = _p.WithholdingTax < 0;
        c.Border(1).BorderColor(ReportDocumentStyle.BorderColor).Column(col =>
        {
            col.Item().Element(c2 => PayslipSections.SectionHeader(c2, "YEAR-END TAX ADJUSTMENT"));
            col.Item().Padding(4).Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(3);
                    cols.RelativeColumn(1);
                });

                PayslipSections.AmountRow(table, isRefund ? "Tax Refund" : "Additional Tax Collected", Math.Abs(_p.WithholdingTax), bold: true);
            });
        });
    }
}
