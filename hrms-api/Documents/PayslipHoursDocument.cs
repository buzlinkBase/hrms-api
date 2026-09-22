using Hrms.Api.Documents.Shared;
using Hrms.Domain.Entities;
using Hrms.Domain.ValueObjects;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Hrms.Api.Documents;

// "Hours" payslip format — same content as PayslipDocument (employee info, deductions, net pay,
// signature — all reused via PayslipSections) but groups Earnings by Work Days / Holiday/s /
// Other Income with hours and amount per row, matching a client-requested layout. No rate column
// is shown — rate is company/category-specific and this format only surfaces hours worked and
// the resulting peso amount. Zero-amount rows are omitted entirely.
public class PayslipHoursDocument : IDocument
{
    private readonly Payroll _p;
    private readonly EmployeeFullModel _e;
    private readonly Company? _company;

    // Payroll doesn't persist the period's actual per-day shift length, only DailyRate — this
    // mirrors the standard 8-hour shift assumption already shown as "Daily Rate" in Employee
    // Info (same as the client's own sample, which reproduces exactly off DailyRate / 8).
    private const decimal StandardShiftHours = 8m;
    private decimal HourlyRate => _p.DailyRate > 0 ? _p.DailyRate / StandardShiftHours : 0m;

    public PayslipHoursDocument(Payroll payroll, EmployeeFullModel employee, Company? company = null)
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

    // ── Row model ────────────────────────────────────────────────────────────

    // Made internal (not private), matching DayTypePayCalculator's convention, so row math is
    // directly testable without rendering a PDF — see hrms.test's InternalsVisibleTo.
    // IsGroupHeader rows ("Regular Days — 12.00 day/s", "Holiday/s — 1.00 day/s", etc.) render
    // as a shaded label-only line; Hours doubles as the day count for those (0 = no count shown,
    // e.g. the "Other Income" heading).
    internal readonly record struct EarningsRow(string Label, decimal Hours, decimal Amount, bool ShowHours, bool IsGroupHeader = false);

    // Worked-only base pay for a day-type category, no OT/premium — Legal/RestLegal/DoubleLegal/
    // RestDoubleLegal's {Category}Pay is worked+unworked blended, so their unworked amount is
    // subtracted here to avoid double-counting against the separate merged unworked row.
    // ND hours are folded in too: {Category}NDBasePay already isolates the day-type-rate-only
    // portion of those hours (Value - NDPremium, no night premium), which is priced at the
    // exact same rate as the plain hours here — the night premium itself is shown separately in
    // the merged "Night Diff." row below, so this is additive, not double-counting the premium.
    // Without this, a category whose hours were mostly worked at night (common for a guard) would
    // show an understated hours/amount here.
    //
    // OT and NDOT hours are NOT folded in here — they get their own "{Category} OT" row instead
    // (see OtRow below), and under both Compounded and Additive modes those buckets carry no
    // day-type-rate portion of their own to fold in (see OvertimeCategoryPolicies/
    // NightDiffOTCategoryPolicies' own doc comments).
    private EarningsRow? BaseRow(string label, decimal hours, decimal pay, decimal ndHours = 0m, decimal ndBasePay = 0m, decimal unworkedPay = 0m)
    {
        var totalHours = hours + ndHours;
        var totalAmount = (pay - unworkedPay) + ndBasePay;
        if (totalHours <= 0 || totalAmount == 0m) return null;
        return new EarningsRow(label, totalHours, totalAmount, true);
    }

    // OT and NDOT hours/base-pay for the same category always use the identical raw OT rate
    // (OVERTIME for Regular, HOLIDAY_OT for every other category — see
    // OvertimeRateStrategies.ResolveRawOtRate), so they merge into a single row.
    private EarningsRow? OtRow(string label, decimal otHours, decimal ndotHours, decimal otBasePay, decimal ndotBasePay)
    {
        var hours = otHours + ndotHours;
        var amount = otBasePay + ndotBasePay;
        if (hours <= 0 || amount == 0m) return null;
        return new EarningsRow(label, hours, amount, true);
    }

    // One merged "Paid Holiday (Unworked)" row across every legal-holiday-involving category —
    // Legal/Rest+Legal always at the 1.0x DOLE multiplier, Double Legal/Rest+Double Legal at
    // 2.0x (a second legal holiday still requires full pay even unworked). Hours are back-derived
    // per category at its own multiplier, then summed, since Payroll has no separate "unworked
    // hours" field and the multipliers differ.
    private EarningsRow? MergedUnworkedRow()
    {
        var amount = _p.LegalHolidayUnworkedPay + _p.RestLegalUnworkedPay + _p.DoubleLegalUnworkedPay + _p.RestDoubleLegalUnworkedPay;
        if (amount == 0m || HourlyRate <= 0m) return null;
        var hours = _p.LegalHolidayUnworkedPay / (HourlyRate * 1.00m)
            + _p.RestLegalUnworkedPay / (HourlyRate * 1.00m)
            + _p.DoubleLegalUnworkedPay / (HourlyRate * 2.00m)
            + _p.RestDoubleLegalUnworkedPay / (HourlyRate * 2.00m);
        return new EarningsRow("Paid Holiday (Unworked)", hours, amount, true);
    }

    // "Night Diff." — one merged row across all 8 categories' ND + NDOT hours/premium, shown
    // once under Regular Days regardless of which category the hours actually fall under.
    private EarningsRow? NightDiffRow()
    {
        var hours = _p.RegularNDHours + _p.RegularNDOTHours
            + _p.RestDayNDHours + _p.RestDayNDOTHours
            + _p.LegalHolNightDiffHours + _p.LegalHolNightDiffOTHours
            + _p.SpecialHolNightDiffHours + _p.SpecialHolNightDiffOTHours
            + _p.RestLegalDayNDHours + _p.RestLegalDayNDOTHours
            + _p.RestSpecialDayNDHours + _p.RestSpecialDayNDOTHours
            + _p.DoubleLegalNDHours + _p.DoubleLegalNDOTHours
            + _p.RestDoubleLegalNDHours + _p.RestDoubleLegalNDOTHours;
        var amount = _p.RegularNDPremiumPay + _p.RegularNDOTPremiumPay
            + _p.RestDayNDPremiumPay + _p.RestDayNDOTPremiumPay
            + _p.LegalNDPremiumPay + _p.LegalNDOTPremiumPay
            + _p.SpecialNDPremiumPay + _p.SpecialNDOTPremiumPay
            + _p.RestLegalNDPremiumPay + _p.RestLegalNDOTPremiumPay
            + _p.RestSpecialNDPremiumPay + _p.RestSpecialNDOTPremiumPay
            + _p.DoubleLegalNDPremiumPay + _p.DoubleLegalNDOTPremiumPay
            + _p.RestDoubleLegalNDPremiumPay + _p.RestDoubleLegalNDOTPremiumPay;
        if (hours <= 0 || amount == 0m) return null;
        return new EarningsRow("Night Diff.", hours, amount, true);
    }

    internal List<EarningsRow> BuildRows()
    {
        var rows = new List<EarningsRow>();
        void Add(EarningsRow? row) { if (row.HasValue) rows.Add(row.Value); }
        void AddPlain(string label, decimal amount) { if (amount != 0m) rows.Add(new EarningsRow(label, 0, amount, false)); }
        void Header(string label, decimal dayCount = 0m) => rows.Add(new EarningsRow(label, dayCount, 0, false, true));

        Header("Total Number of Work Days", PayslipSections.DaysWorked(_p));
        Header("Regular Days", PayslipSections.RegularDaysWorked(_p));

        // Regular — VARIABLE reproduces BasicPay exactly as hours x hourly; FIXED's BasicPay
        // (MonthlyRate/divisor) isn't hours-driven, so it's shown as a plain amount.
        if (_p.SalaryType == SalaryType.FIXED)
        {
            var fixedBasic = _p.BasicPay - _p.PaidLeaves;
            if (fixedBasic != 0m) rows.Add(new EarningsRow("Basic Pay", 0, fixedBasic, false));
        }
        else
        {
            Add(BaseRow("Regular Hours", _p.RegularNetHours, _p.RegularNetHours * HourlyRate, _p.RegularNDHours, _p.RegularNDBasePay));
            Add(OtRow("Regular OT Hours", _p.RegularOTHours, _p.RegularNDOTHours, _p.RegularOTBasePay, _p.RegularNDOTBasePay));
        }

        Add(BaseRow("Rest Day", _p.RestDayHours, _p.RestDayPay, _p.RestDayNDHours, _p.RestDayNDBasePay));
        Add(OtRow("Rest Day OT Hours", _p.RestDayOTHours, _p.RestDayNDOTHours, _p.RestDayOTBasePay, _p.RestDayNDOTBasePay));

        Add(NightDiffRow());
        AddPlain("Paid Leave", _p.PaidLeaves);

        Header("Holiday/s", PayslipSections.HolidayDaysWorked(_p));
        Add(MergedUnworkedRow());
        Add(BaseRow("Legal Holiday (Worked)", _p.LegalHolHours, _p.LegalPay, _p.LegalHolNightDiffHours, _p.LegalNDBasePay, _p.LegalHolidayUnworkedPay));
        Add(OtRow("Legal Holiday OT", _p.LegalHolOTHours, _p.LegalHolNightDiffOTHours, _p.LegalOTBasePay, _p.LegalNDOTBasePay));
        Add(BaseRow("Special Holiday", _p.SpecialHolHours, _p.SpecialPay, _p.SpecialHolNightDiffHours, _p.SpecialNDBasePay));
        Add(OtRow("Special Holiday OT", _p.SpecialHolOTHours, _p.SpecialHolNightDiffOTHours, _p.SpecialOTBasePay, _p.SpecialNDOTBasePay));
        Add(BaseRow("Rest Day + Legal Holiday (Worked)", _p.RestLegalDayHours, _p.RestLegalPay, _p.RestLegalDayNDHours, _p.RestLegalNDBasePay, _p.RestLegalUnworkedPay));
        Add(OtRow("Rest Day + Legal Holiday OT", _p.RestLegalDayOTHours, _p.RestLegalDayNDOTHours, _p.RestLegalOTBasePay, _p.RestLegalNDOTBasePay));
        Add(BaseRow("Rest Day + Special Holiday", _p.RestSpecialDayHours, _p.RestSpecialPay, _p.RestSpecialDayNDHours, _p.RestSpecialNDBasePay));
        Add(OtRow("Rest Day + Special Holiday OT", _p.RestSpecialDayOTHours, _p.RestSpecialDayNDOTHours, _p.RestSpecialOTBasePay, _p.RestSpecialNDOTBasePay));
        Add(BaseRow("Double Legal Holiday (Worked)", _p.DoubleLegalHours, _p.DoubleLegalPay, _p.DoubleLegalNDHours, _p.DoubleLegalNDBasePay, _p.DoubleLegalUnworkedPay));
        Add(OtRow("Double Legal Holiday OT", _p.DoubleLegalOTHours, _p.DoubleLegalNDOTHours, _p.DoubleLegalOTBasePay, _p.DoubleLegalNDOTBasePay));
        Add(BaseRow("Rest Day + Double Legal Holiday (Worked)", _p.RestDoubleLegalHours, _p.RestDoubleLegalPay, _p.RestDoubleLegalNDHours, _p.RestDoubleLegalNDBasePay, _p.RestDoubleLegalUnworkedPay));
        Add(OtRow("Rest Day + Double Legal Holiday OT", _p.RestDoubleLegalOTHours, _p.RestDoubleLegalNDOTHours, _p.RestDoubleLegalOTBasePay, _p.RestDoubleLegalNDOTBasePay));

        // Non-DTR income — no hours breakdown applies to these (not attendance-priced), so
        // they're plain amount rows, same treatment as the FIXED Basic Pay row above. Included
        // so GROSS INCOME at the bottom reconciles with the rows shown above it, rather than
        // silently including money none of the itemized rows account for.
        Header("Other Income");
        AddPlain("One-Time Leave Payout (Company)", _p.CompanyFundedLeavePay);
        AddPlain("COLA", _p.Cola);
        AddPlain("Regular Allowances", _p.TotalRegularAllowances);
        AddPlain("Bonuses", _p.TotalBonuses);
        AddPlain("Commissions", _p.TotalCommissions);
        AddPlain("De Minimis", _p.TotalDeminimises);
        AddPlain("Reimbursement", _p.Reimbursement);
        AddPlain("Other Income", _p.TotalOtherIncome);

        return rows;
    }

    // ── Compose ──────────────────────────────────────────────────────────────

    void ComposeEarnings(IContainer c)
    {
        // 13th Month/Last Pay/Year-End Adjustment are lump-sum, not attendance-driven — no
        // hours breakdown exists for them, so they fall back to the same lump-sum figure
        // PayslipDocument shows for these PayrollTypes.
        c.Border(1).BorderColor(ReportDocumentStyle.BorderColor).Column(col =>
        {
            col.Item().Element(c2 => PayslipSections.SectionHeader(c2, "EARNINGS"));

            if (_p.PayrollType != PayrollType.Regular)
            {
                col.Item().Padding(4).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(3);
                        cols.RelativeColumn(1);
                    });
                    var label = _p.PayrollType switch
                    {
                        PayrollType.ThirteenthMonth => "13th Month Pay",
                        PayrollType.LastPay => "Prorated 13th Month + Leave Conversion",
                        _ => _p.WithholdingTax < 0 ? "Tax Refund" : "Additional Tax Collected",
                    };
                    var amount = _p.PayrollType == PayrollType.YearEndAdjustment ? Math.Abs(_p.WithholdingTax) : _p.GrossIncome;
                    PayslipSections.AmountRow(table, label, amount, bold: true);
                });
                return;
            }

            var rows = BuildRows();
            col.Item().Padding(4).Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(2.6f);
                    cols.RelativeColumn(1.2f);
                    cols.RelativeColumn(1.4f);
                });

                foreach (var row in rows)
                {
                    if (row.IsGroupHeader)
                    {
                        // Grayish, not the teal TableHeaderBg used for data rows elsewhere —
                        // matches the same neutral tone as the "EARNINGS" section bar above
                        // (ReportDocumentStyle.SectionHeaderBg), just for a nested sub-heading.
                        table.Cell().Background(ReportDocumentStyle.SectionHeaderBg).Padding(3).Text(row.Label).Bold().FontSize(8.5f);
                        table.Cell().Background(ReportDocumentStyle.SectionHeaderBg).Padding(3).Text(row.Hours > 0 ? $"{row.Hours:N2} day/s" : "").FontSize(8.5f).FontColor(ReportDocumentStyle.LabelColor);
                        table.Cell().Background(ReportDocumentStyle.SectionHeaderBg).Padding(3).Text("");
                        continue;
                    }
                    table.Cell().PaddingVertical(3).PaddingLeft(14).Text(row.Label);
                    table.Cell().PaddingVertical(3).Text(row.ShowHours ? $"{row.Hours:N2}" : "—").FontColor(ReportDocumentStyle.LabelColor);
                    table.Cell().PaddingVertical(3).AlignRight().Text(PayslipSections.Money(row.Amount));
                }

                table.Cell().Padding(3).BorderTop(1).BorderColor(ReportDocumentStyle.BorderColor).Text("GROSS INCOME").Bold().FontColor(ReportDocumentStyle.Primary);
                table.Cell().Padding(3).BorderTop(1).BorderColor(ReportDocumentStyle.BorderColor).Text("");
                table.Cell().Padding(3).BorderTop(1).BorderColor(ReportDocumentStyle.BorderColor).AlignRight().Text(PayslipSections.Money(_p.GrossIncome)).Bold().FontColor(ReportDocumentStyle.Primary);
            });
        });
    }
}
