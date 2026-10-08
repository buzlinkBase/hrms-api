using Hrms.Api.Documents.Shared;
using Hrms.Domain.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Hrms.Api.Documents;

public class PayrollSummaryReportDocument : IDocument
{
    private readonly List<Payroll> _rows;
    private readonly DateOnly _from;
    private readonly DateOnly _to;
    private readonly Company? _company;
    private readonly Dictionary<Guid, string> _clientNames;

    // Printouts always render in black, regardless of the app's teal-green UI theme.
    private static readonly string Primary = "#000000";
    private static readonly string TableHeaderBg = "#e8f5f1";
    private static readonly string BorderColor = "#d9d9d9";
    private static readonly string LabelColor = "#666666";
    private static readonly string TextColor = "#1a1a1a";

    private string CompanyName => ReportDocumentStyle.CompanyName(_company);

    public PayrollSummaryReportDocument(List<Payroll> rows, DateOnly from, DateOnly to, Company? company = null, Dictionary<Guid, string>? clientNames = null)
    {
        _rows = rows;
        _from = from;
        _to = to;
        _company = company;
        _clientNames = clientNames ?? new();
    }

    public DocumentMetadata GetMetadata() => new DocumentMetadata
    {
        Title = $"Payroll Summary - {_from:MMM dd} to {_to:MMM dd, yyyy}",
        Author = CompanyName,
        CreationDate = DateTimeOffset.UtcNow,
    };

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            // A1 landscape (rather than A3) at 8pt -- every Earnings and Deductions figure the
            // Payroll Summary screen shows is printed (see Columns), 37 columns in all, too wide
            // for A3 to stay legible.
            page.Size(PageSizes.A1.Landscape());
            page.MarginTop(1.2f, Unit.Centimetre);
            page.MarginBottom(1.2f, Unit.Centimetre);
            page.MarginHorizontal(1.2f, Unit.Centimetre);
            page.DefaultTextStyle(x => x.FontSize(8).FontColor(TextColor));

            page.Header().Element(ComposePageHeader);
            page.Content().PaddingTop(8).Element(ComposeTable);
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
                col.Item().Text("PAYROLL SUMMARY REPORT").FontSize(9).FontColor(LabelColor);
                if (!string.IsNullOrWhiteSpace(_company?.Address) || !string.IsNullOrWhiteSpace(_company?.Contact))
                {
                    col.Item().Text(string.Join("  •  ", new[] { _company?.Address, _company?.Contact }
                        .Where(s => !string.IsNullOrWhiteSpace(s))))
                        .FontSize(7.5f).FontColor(LabelColor);
                }
            });
            row.ConstantItem(220).AlignRight().Column(col =>
            {
                col.Item().Text($"{_from:MMM dd} – {_to:MMM dd, yyyy}").Bold().FontSize(10);
                col.Item().Text($"{_rows.Count} record(s)").FontSize(8).FontColor(LabelColor);
            });
        });
    }

    // Client name for a row — Payroll only stores ClientId (a snapshot, not a navigation
    // property), so names are resolved once in the controller and passed in as a lookup rather
    // than joined per row here. "—" for employees with no client assigned.
    private string ClientNameFor(Payroll r) =>
        r.ClientId is { } id && _clientNames.TryGetValue(id, out var name) ? name : "—";

    // Hour totals matching each existing peso column's own scope exactly (see
    // EmployeePayrollLineService.GetBasicPay / BasicPayrollModel.Calculate):
    // - Basic: Regular category only (BasicPay = Sum(RegularDayPay)) — meaningless for FIXED
    //   salary (no hours drive a flat monthly rate), so left at 0 for those rows.
    // - OT/ND: all 8 day-type categories' OT/ND hours, as stored (natural: NDOT hours -- both
    //   OT and ND -- are already inside BOTH columns). The NDOT pay folds into both columns only
    //   for a row generated under Additive mode — see OtPay/NdPay below for why.
    // - Holiday: the 6 holiday-involving categories' plain worked hours only, matching
    //   HolidayPay's own scope (which explicitly excludes their OT/ND/NDOT pay too, in either
    //   mode — the OT/ND/NDOT pricing (Calculators/DtrPay) never touches HolidayPay's inputs).
    internal static bool IsAdditive(Payroll r) => r.OtNdCalculationMethod == OtNdCalculationMethod.Additive;

    internal static decimal BasicHours(Payroll r) =>
        r.SalaryType == SalaryType.FIXED ? 0m : r.RegularNetHours;

    // NDOT's raw-OT-rate portion (the flat OT base, mode-independent — see
    // Calculators/DtrPay's AdditiveDtrPayCalculator) and ND-delta portion (the flat ND premium)
    // are exactly what NDOT's Value decomposes into under Additive mode (dayRate dropped, only
    // otRate + (ndRate - 1) applies) — under Compounded mode NDOT's Value has a genuine 3-way
    // day-rate x OT x ND cross-term that these two pieces alone don't reconstruct, so folding
    // them in would misstate Compounded rows. That's why this only ever applies when IsAdditive.
    internal static decimal NdotOtPortionPay(Payroll r) =>
        r.RegularNDOTBasePay + r.RestDayNDOTBasePay + r.LegalNDOTBasePay + r.SpecialNDOTBasePay +
        r.RestLegalNDOTBasePay + r.RestSpecialNDOTBasePay + r.DoubleLegalNDOTBasePay + r.RestDoubleLegalNDOTBasePay;

    internal static decimal NdotNdPortionPay(Payroll r) =>
        r.RegularNDOTPremiumPay + r.RestDayNDOTPremiumPay + r.LegalNDOTPremiumPay + r.SpecialNDOTPremiumPay +
        r.RestLegalNDOTPremiumPay + r.RestSpecialNDOTPremiumPay + r.DoubleLegalNDOTPremiumPay + r.RestDoubleLegalNDOTPremiumPay;

    // OT and ND hours are stored natural (OT and ND each already include the NDOT hours -- see
    // dtr-api DailyRecordBuilder), so they're summed as-is in both modes.
    internal static decimal OtHours(Payroll r) =>
        r.RegularOTHours + r.RestDayOTHours + r.LegalHolOTHours + r.SpecialHolOTHours +
        r.RestLegalDayOTHours + r.RestSpecialDayOTHours + r.DoubleLegalOTHours + r.RestDoubleLegalOTHours;

    internal static decimal NdHours(Payroll r) =>
        r.RegularNDHours + r.RestDayNDHours + r.LegalHolNightDiffHours + r.SpecialHolNightDiffHours +
        r.RestLegalDayNDHours + r.RestSpecialDayNDHours + r.DoubleLegalNDHours + r.RestDoubleLegalNDHours;

    // OvertimePay is already mode-aware (the OT-only policy branches its own Value on the mode)
    // -- only the NDOT top-up needs to be conditional here, so a Compounded-mode row keeps
    // reading exactly r.OvertimePay, unchanged from before this feature. Mirrors
    // PayslipHoursDocument's "{Category} OT" row exactly, since the flat OT base (what
    // {cat}OTBasePay/NDOTBasePay hold) is itself mode-independent.
    internal static decimal OtPay(Payroll r) => r.OvertimePay + (IsAdditive(r) ? NdotOtPortionPay(r) : 0m);

    // Regular category's ND-only bucket's day-rate portion ({cat}NDBasePay) -- under Additive
    // mode, this is what PayslipHoursDocument's BaseRow folds into "Regular Hours" instead of
    // leaving it in "Night Diff.". Only Regular has a fold destination here (Basic is
    // Regular-only by definition); the 6 holiday categories fold into Holiday instead (see
    // HolidayNdDayRatePortion below), and plain Rest Day's ND bucket has no separate
    // Basic/Holiday-style column to fold into, so it deliberately stays untouched in both
    // BasicPay/HolidayPay and NdPay below.
    internal static decimal RegularNdDayRatePortion(Payroll r) => r.RegularNDBasePay;

    internal static decimal HolidayNdDayRatePortion(Payroll r) =>
        r.LegalNDBasePay + r.RestLegalNDBasePay + r.SpecialNDBasePay + r.RestSpecialNDBasePay +
        r.DoubleLegalNDBasePay + r.RestDoubleLegalNDBasePay;

    // The same 7 categories' ND-only premium ({cat}NDPremiumPay) -- what's left in the ND
    // column under Additive once each category's day-rate portion above has moved to
    // Basic/Holiday. Excludes plain Rest Day (RestDayNDPay stays as its own full blended
    // figure below, unmoved).
    internal static decimal NdOnlyPremiumPortion(Payroll r) =>
        r.RegularNDPremiumPay + r.LegalNDPremiumPay + r.RestLegalNDPremiumPay + r.SpecialNDPremiumPay +
        r.RestSpecialNDPremiumPay + r.DoubleLegalNDPremiumPay + r.RestDoubleLegalNDPremiumPay;

    // "Basic"/"Holiday" as shown on this report specifically -- paired 1:1 with OtPay/NdPay/
    // Rest Day/Holiday as a full decomposition of Gross Income. Under Additive mode this folds
    // in each category's ND-hours day-rate portion, matching the Hours payslip's "Regular
    // Hours"/"Special Holiday" rows exactly (PayslipHoursDocument.BaseRow does the identical
    // fold, in both modes, for the payslip -- it's only conditional here because the
    // non-folded figures already reconcile to Gross under Compounded on their own, and folding
    // them there too would just be cosmetic). Compounded mode is byte-for-byte unchanged.
    internal static decimal BasicPay(Payroll r) =>
        r.BasicPay + (IsAdditive(r) ? RegularNdDayRatePortion(r) : 0m);

    internal static decimal HolidayPay(Payroll r) =>
        r.HolidayPay + (IsAdditive(r) ? HolidayNdDayRatePortion(r) : 0m);

    // Under Compounded, unchanged: the full blended ND-only bucket value across all 8
    // categories (day-rate + premium together), same as always. Under Additive, becomes
    // "premium only" for the 7 categories that now have their day-rate portion living in
    // Basic/Holiday instead (see above), plus plain Rest Day's still-full blended value (no
    // fold destination for it) and NDOT's own ND delta -- this exact rebalancing moves money
    // between columns without changing the row's total, so Basic + OT + ND + Holiday + Rest
    // Day still reconciles to Gross Income in both modes.
    internal static decimal NdPay(Payroll r) =>
        IsAdditive(r)
            ? NdOnlyPremiumPortion(r) + r.RestDayNDPay + NdotNdPortionPay(r)
            : r.NightDifferentialPay;

    internal static decimal HolidayHours(Payroll r) =>
        r.LegalHolHours + r.SpecialHolHours + r.RestLegalDayHours + r.RestSpecialDayHours +
        r.DoubleLegalHours + r.RestDoubleLegalHours;

    // 2 decimals like every other DTR hours display, so minute-level time shows (62 min OT = 1.03).
    internal static string Hours(decimal v) => v == 0 ? "—" : v.ToString("N2");

    // Same figure as the screen's Earnings "Rest Day" column (payroll-summary.util.ts
    // restDayTotal): plain Rest Day's own four buckets. The rest-day-plus-holiday categories
    // live in Holiday instead.
    internal static decimal RestDayPay(Payroll r) =>
        r.RestDayPay + r.RestDayOTPay + r.RestDayNDPay + r.RestDayNDOTPay;

    // Same rule as the screen's "Paid Leave" column: FIXED's Basic Pay already covers
    // company-paid leave days, so only the informational non-company slice is shown for it;
    // VARIABLE's Basic Pay never includes leave-day pay, so the full amount is shown.
    internal static decimal PaidLeavePay(Payroll r) =>
        r.SalaryType == SalaryType.FIXED ? r.NonCompanyPaidLeaves : r.PaidLeaves;

    // Informational, as on the screen's Deductions tab -- already taken out of Basic, not part
    // of TotalDeductions.
    internal static decimal LateUndertimeAmount(Payroll r) => r.LateAmount + r.UnderTimeAmount;

    // Scheduled deductions other than loans. Cash Bond is a separate figure, never inside
    // OtherDeductions (see EmployeePayrollLineService.ComputeDeductions).
    internal static decimal OtherDeductionsExcludingLoans(Payroll r) => r.OtherDeductions - r.TotalLoans;

    private enum ColumnKind { Text, Hours, Money }

    // One entry per printed column, in print order. The header, every data row and the TOTAL
    // row are all built from this list, so a column can't be added to one and missed in the
    // others. Mirrors the screen's Earnings and Deductions & Net tabs.
    private sealed record ReportColumn(
        string Header,
        float Width,
        ColumnKind Kind,
        Func<Payroll, decimal>? Amount = null,
        Func<Payroll, string>? Label = null,
        bool Muted = false,
        bool Emphasis = false);

    internal IReadOnlyList<string> ColumnHeaders => Columns().Select(x => x.Header).ToList();

    private IReadOnlyList<ReportColumn> Columns() => new ReportColumn[]
    {
        new("Employee", 2.2f, ColumnKind.Text, Label: r => r.FullName),
        new("Client", 1.6f, ColumnKind.Text, Label: ClientNameFor, Muted: true),
        new("Payroll Date", 1.3f, ColumnKind.Text, Label: r => r.PayrollDate.ToString("MMM dd, yyyy"), Muted: true),

        // Earnings
        new("Basic Hrs", 0.7f, ColumnKind.Hours, BasicHours, Muted: true),
        new("Basic", 1, ColumnKind.Money, BasicPay),
        new("OT Hrs", 0.7f, ColumnKind.Hours, OtHours, Muted: true),
        new("OT", 1, ColumnKind.Money, OtPay),
        new("ND Hrs", 0.7f, ColumnKind.Hours, NdHours, Muted: true),
        new("ND", 1, ColumnKind.Money, NdPay),
        // Always shown, in both OT/ND modes. Under Additive this pay is already folded into the
        // OT/ND columns above, so it is informational there.
        new("NDOT", 1, ColumnKind.Money, r => r.NightDifferentialOTPay),
        new("Holiday Hrs", 0.7f, ColumnKind.Hours, HolidayHours, Muted: true),
        new("Holiday", 1, ColumnKind.Money, HolidayPay),
        new("Rest Day", 1, ColumnKind.Money, RestDayPay),
        new("Paid Leave", 1, ColumnKind.Money, PaidLeavePay),
        new("COLA", 1, ColumnKind.Money, r => r.Cola),
        new("Allow.", 1, ColumnKind.Money, r => r.TotalRegularAllowances),
        new("Bonuses", 1, ColumnKind.Money, r => r.TotalBonuses),
        new("Commissions", 1, ColumnKind.Money, r => r.TotalCommissions),
        new("De Minimis", 1, ColumnKind.Money, r => r.TotalDeminimises),
        new("Other Inc.", 1, ColumnKind.Money, r => r.TotalOtherIncome),
        new("1x Payout (Co)", 1, ColumnKind.Money, r => r.CompanyFundedLeavePay),
        new("1x Payout (Gov)", 1, ColumnKind.Money, r => r.GovernmentFundedLeavePay),
        new("Reimb.", 1, ColumnKind.Money, r => r.Reimbursement),
        new("Gross", 1.1f, ColumnKind.Money, r => r.GrossIncome, Emphasis: true),
        // Informational, excluded from Gross and Net.
        new("Ret. Accrual", 1, ColumnKind.Money, r => r.RetirementAccrual, Muted: true),

        // Deductions & Net
        new("SSS", 1, ColumnKind.Money, r => r.SSSContribution),
        new("PhilHealth", 1, ColumnKind.Money, r => r.PhilHealthContribution),
        new("Pag-IBIG", 1, ColumnKind.Money, r => r.PagIbigContribution),
        new("W-Tax", 1, ColumnKind.Money, r => r.WithholdingTax),
        new("Cash Bond", 1, ColumnKind.Money, r => r.CashBondDeduction),
        new("Loans", 1, ColumnKind.Money, r => r.TotalLoans),
        new("Other Ded.", 1, ColumnKind.Money, OtherDeductionsExcludingLoans),
        new("Total Ded.", 1.1f, ColumnKind.Money, r => r.TotalDeductions, Emphasis: true),
        new("Late/UT", 1, ColumnKind.Money, LateUndertimeAmount, Muted: true),
        new("Absent", 1, ColumnKind.Money, r => r.AbsencesAmount, Muted: true),
        // Last Pay only -- the cashed-out Retirement Fund balance, added straight to Net.
        new("Ret. Payout", 1, ColumnKind.Money, r => r.RetirementPayout),
        new("Net Pay", 1.2f, ColumnKind.Money, r => r.NetPay, Emphasis: true),
    };

    private static string Money(decimal v) => v.ToString("N2");

    private static string Format(ReportColumn col, decimal v) =>
        col.Kind == ColumnKind.Hours ? Hours(v) : Money(v);

    void ComposeTable(IContainer c)
    {
        var columns = Columns();

        c.Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                foreach (var col in columns)
                    cols.RelativeColumn(col.Width);
            });

            static IContainer HeaderCell(IContainer c) =>
                c.Background(TableHeaderBg).Padding(3);
            static IContainer DataCell(IContainer c) =>
                c.BorderBottom(1).BorderColor("#eeeeee").Padding(3);
            static IContainer TotalCell(IContainer c) =>
                c.BorderTop(1).BorderColor(BorderColor).Padding(3);

            table.Header(header =>
            {
                foreach (var col in columns)
                {
                    var cell = header.Cell().Element(HeaderCell);
                    if (col.Kind != ColumnKind.Text) cell = cell.AlignRight();
                    cell.Text(col.Header).Bold();
                }
            });

            foreach (var r in _rows)
            {
                foreach (var col in columns)
                {
                    var cell = table.Cell().Element(DataCell);
                    if (col.Kind != ColumnKind.Text) cell = cell.AlignRight();
                    var text = cell.Text(col.Kind == ColumnKind.Text ? col.Label!(r) : Format(col, col.Amount!(r)));
                    if (col.Muted) text.FontColor(LabelColor);
                    if (col.Emphasis) text.Bold().FontColor(Primary);
                }
            }

            for (var i = 0; i < columns.Count; i++)
            {
                var col = columns[i];
                var cell = table.Cell().Element(TotalCell);
                if (col.Kind == ColumnKind.Text)
                {
                    cell.Text(i == 0 ? "TOTAL" : "").Bold();
                    continue;
                }
                cell.AlignRight().Text(Format(col, _rows.Sum(col.Amount!))).Bold().FontColor(Primary);
            }
        });
    }
}
