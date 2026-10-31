using Hrms.Api.Documents.Shared;
using Hrms.Domain.Entities;
using Hrms.Domain.ValueObjects;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Hrms.Api.Documents;

// "Hours" payslip format — the payslip for every OT/ND method (employee info, deductions, net pay,
// signature reused via PayslipSections). Groups Earnings by Work Days / Holiday/s / Other Income
// with hours and amount per row, matching a client-requested layout. No rate column is shown —
// rate is company/category-specific and this format only surfaces hours worked and the resulting
// peso amount. Zero-amount rows are omitted entirely. The OT and Night Diff. amounts follow the
// run's OT/ND method (PayslipHoursAmounts).
public class PayslipHoursDocument : IDocument
{
    private readonly Payroll _p;
    private readonly EmployeeFullModel _e;
    private readonly Company? _company;
    private readonly byte[]? _logo;
    private readonly PayslipHoursAmounts _amounts;

    // Payroll doesn't persist the period's actual per-day shift length, only DailyRate — this
    // mirrors the standard 8-hour shift assumption already shown as "Daily Rate" in Employee
    // Info (same as the client's own sample, which reproduces exactly off DailyRate / 8).
    private const decimal StandardShiftHours = 8m;
    private decimal HourlyRate => _p.DailyRate > 0 ? _p.DailyRate / StandardShiftHours : 0m;

    // What the plain regular hours were actually paid: the per-day RegularDayPay saved with the
    // payroll (TimeHourPayResults). Not BasicPay — salary adjustments are added onto that. A
    // payroll with no per-day breakdown falls back to the daytime hours x hourly, the old
    // behaviour. RegularNetHours is natural (night included), so the night regular hours
    // (ND - NDOT) come off first — their base is RegularNDBasePay, added by BaseRow.
    private decimal RegularPaidAmount
    {
        get
        {
            var days = _p.TimeHourPayResults;
            if (days is { Count: > 0 }) return days.Sum(d => d.RegularDayPay);
            var nightRegularHours = Math.Max(0m, _p.RegularNDHours - _p.RegularNDOTHours);
            return Math.Max(0m, _p.RegularNetHours - nightRegularHours) * HourlyRate;
        }
    }

    private readonly PayslipOtherIncome _otherIncome;

    public PayslipHoursDocument(Payroll payroll, EmployeeFullModel employee, Company? company = null,
        PayslipOtherIncome? otherIncome = null, byte[]? logo = null)
    {
        _logo = logo;
        _p = payroll;
        _e = employee;
        _company = company;
        _otherIncome = otherIncome ?? PayslipOtherIncome.Categories;
        _amounts = PayslipHoursAmounts.For(payroll.OtNdCalculationMethod);
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

            page.Header().Element(c => PayslipSections.ComposePageHeader(c, _p, _e, _company, _logo));
            page.Content().PaddingTop(8).Element(ComposeContent);
            page.Footer().Element(c => ReportHeaderComposer.ComposeFooter(c, _company?.PayslipFooter));
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
    // Hours are the category's natural Hrs (the DTR stores them with their night portion), so no
    // ND hours are added. The amount adds {Category}NDBasePay: the day-type-rate-only portion of
    // those night hours wherever the pay method left it in the ND bucket (Compounded, or a Fixed
    // employee's regular day under Additive) — the night premium itself is shown separately in
    // the merged "Night Diff." row below, so this is additive, not double-counting the premium.
    //
    // OT hours get their own "{Category} OT" row instead (see OtRow below), whose amount is all of
    // the OT and NDOT pay, so nothing of theirs is folded in here.
    private EarningsRow? BaseRow(string label, decimal hours, decimal pay, decimal ndBasePay = 0m, decimal unworkedPay = 0m)
    {
        var totalAmount = (pay - unworkedPay) + ndBasePay;
        if (hours <= 0 || totalAmount == 0m) return null;
        return new EarningsRow(label, hours, totalAmount, true);
    }

    // A category's OT and NDOT merge into a single row. The hours are the natural OT hours, which
    // already include the NDOT hours; the amount depends on the OT/ND method (PayslipHoursAmounts).
    private EarningsRow? OtRow(string label, decimal hours, PayCategoryFigures category)
    {
        var amount = _amounts.Ot(category);
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

    // "Night Diff." — one merged row across all 8 categories' night premium, shown once under
    // Regular Days regardless of which category the hours actually fall under. Which hours and
    // pay count depends on the OT/ND method (PayslipHoursAmounts).
    private EarningsRow? NightDiffRow()
    {
        var categories = PayCategoryFigures.All(_p);
        var hours = categories.Sum(_amounts.NightDiffHours);
        var amount = categories.Sum(_amounts.NightDiff);
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

        // Regular — VARIABLE shows what was actually paid for the plain regular hours
        // (RegularDayPay, the per-day hourly x RegularNetHours sum that BasicPay is built from),
        // so the rows always add up to GROSS INCOME; FIXED's BasicPay (MonthlyRate/divisor)
        // isn't hours-driven, so it's shown as a plain amount.
        if (_p.SalaryType == SalaryType.FIXED)
        {
            var fixedBasic = _p.BasicPay - _p.PaidLeaves;
            if (fixedBasic != 0m) rows.Add(new EarningsRow("Basic Pay", 0, fixedBasic, false));
        }
        else
        {
            Add(BaseRow("Regular Hours", _p.RegularNetHours, RegularPaidAmount, _p.RegularNDBasePay));
            Add(OtRow("Regular OT Hours", _p.RegularOTHours, PayCategoryFigures.Regular(_p)));
        }

        Add(BaseRow("Rest Day", _p.RestDayHours, _p.RestDayPay, _p.RestDayNDBasePay));
        Add(OtRow("Rest Day OT Hours", _p.RestDayOTHours, PayCategoryFigures.RestDay(_p)));

        Add(NightDiffRow());
        AddPlain("Paid Leave", _p.PaidLeaves);

        Header("Holiday/s", PayslipSections.HolidayDaysWorked(_p));
        Add(MergedUnworkedRow());
        Add(BaseRow("Legal Holiday (Worked)", _p.LegalHolHours, _p.LegalPay, _p.LegalNDBasePay, _p.LegalHolidayUnworkedPay));
        Add(OtRow("Legal Holiday OT", _p.LegalHolOTHours, PayCategoryFigures.Legal(_p)));
        Add(BaseRow("Special Holiday", _p.SpecialHolHours, _p.SpecialPay, _p.SpecialNDBasePay));
        Add(OtRow("Special Holiday OT", _p.SpecialHolOTHours, PayCategoryFigures.Special(_p)));
        Add(BaseRow("Rest Day + Legal Holiday (Worked)", _p.RestLegalDayHours, _p.RestLegalPay, _p.RestLegalNDBasePay, _p.RestLegalUnworkedPay));
        Add(OtRow("Rest Day + Legal Holiday OT", _p.RestLegalDayOTHours, PayCategoryFigures.RestLegal(_p)));
        Add(BaseRow("Rest Day + Special Holiday", _p.RestSpecialDayHours, _p.RestSpecialPay, _p.RestSpecialNDBasePay));
        Add(OtRow("Rest Day + Special Holiday OT", _p.RestSpecialDayOTHours, PayCategoryFigures.RestSpecial(_p)));
        Add(BaseRow("Double Legal Holiday (Worked)", _p.DoubleLegalHours, _p.DoubleLegalPay, _p.DoubleLegalNDBasePay, _p.DoubleLegalUnworkedPay));
        Add(OtRow("Double Legal Holiday OT", _p.DoubleLegalOTHours, PayCategoryFigures.DoubleLegal(_p)));
        Add(BaseRow("Rest Day + Double Legal Holiday (Worked)", _p.RestDoubleLegalHours, _p.RestDoubleLegalPay, _p.RestDoubleLegalNDBasePay, _p.RestDoubleLegalUnworkedPay));
        Add(OtRow("Rest Day + Double Legal Holiday OT", _p.RestDoubleLegalOTHours, PayCategoryFigures.RestDoubleLegal(_p)));

        // Non-DTR income — no hours breakdown applies to these (not attendance-priced), so
        // they're plain amount rows, same treatment as the FIXED Basic Pay row above. Included
        // so GROSS INCOME at the bottom reconciles with the rows shown above it, rather than
        // silently including money none of the itemized rows account for.
        Header("Other Income");
        AddPlain("One-Time Leave Payout (Company)", _p.CompanyFundedLeavePay);
        AddPlain("COLA", _p.Cola);
        foreach (var line in _otherIncome.Rows(_p))
            AddPlain(line.Label, line.Amount);

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
                    if (_p.PayrollType == PayrollType.LastPay)
                    {
                        PayslipSections.LastPayLumpSumRows(table, _p, _otherIncome);
                        return;
                    }
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
