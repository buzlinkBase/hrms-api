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
            // A1 landscape (rather than A3) — the Client/Payroll Date columns and the four
            // per-category hour columns (Basic/OT/ND/Holiday) bring this to 23 columns total,
            // too wide for A3 to stay legible.
            page.Size(PageSizes.A1.Landscape());
            page.MarginTop(1.2f, Unit.Centimetre);
            page.MarginBottom(1.2f, Unit.Centimetre);
            page.MarginHorizontal(1.2f, Unit.Centimetre);
            page.DefaultTextStyle(x => x.FontSize(9).FontColor(TextColor));

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
    // - OT/ND: all 8 day-type categories' OT-only/ND-only hours. NDOT (hours that are BOTH OT
    //   and ND) folds into BOTH columns, but only for a row generated under Additive mode — see
    //   OtHours/NdHours/OtPay/NdPay below for why.
    // - Holiday: the 6 holiday-involving categories' plain worked hours only, matching
    //   HolidayPay's own scope (which explicitly excludes their OT/ND/NDOT pay too, in either
    //   mode — NightDiffOTCategoryPolicies never touches HolidayPay's inputs).
    internal static bool IsAdditive(Payroll r) => r.OtNdCalculationMethod == OtNdCalculationMethod.Additive;

    internal static decimal BasicHours(Payroll r) =>
        r.SalaryType == SalaryType.FIXED ? 0m : r.RegularNetHours;

    internal static decimal NdotHoursTotal(Payroll r) =>
        r.RegularNDOTHours + r.RestDayNDOTHours + r.LegalHolNightDiffOTHours + r.SpecialHolNightDiffOTHours +
        r.RestLegalDayNDOTHours + r.RestSpecialDayNDOTHours + r.DoubleLegalNDOTHours + r.RestDoubleLegalNDOTHours;

    // NDOT's raw-OT-rate portion (FlatOvertimeBase, mode-independent — see
    // NightDiffOTCategoryPolicies' own doc comment) and ND-delta portion (FlatNightDiffPremium)
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

    internal static decimal OtHours(Payroll r) =>
        r.RegularOTHours + r.RestDayOTHours + r.LegalHolOTHours + r.SpecialHolOTHours +
        r.RestLegalDayOTHours + r.RestSpecialDayOTHours + r.DoubleLegalOTHours + r.RestDoubleLegalOTHours
        + (IsAdditive(r) ? NdotHoursTotal(r) : 0m);

    internal static decimal NdHours(Payroll r) =>
        r.RegularNDHours + r.RestDayNDHours + r.LegalHolNightDiffHours + r.SpecialHolNightDiffHours +
        r.RestLegalDayNDHours + r.RestSpecialDayNDHours + r.DoubleLegalNDHours + r.RestDoubleLegalNDHours
        + (IsAdditive(r) ? NdotHoursTotal(r) : 0m);

    // OvertimePay is already mode-aware (the OT-only policy branches its own Value on the mode)
    // -- only the NDOT top-up needs to be conditional here, so a Compounded-mode row keeps
    // reading exactly r.OvertimePay, unchanged from before this feature. Mirrors
    // PayslipHoursDocument's "{Category} OT" row exactly, since FlatOvertimeBase (what
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

    void ComposeTable(IContainer c)
    {
        static string Money(decimal v) => v.ToString("N2");
        static string Hours(decimal v) => v == 0 ? "—" : v.ToString("N1");

        c.Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.RelativeColumn(2.2f); // Employee
                cols.RelativeColumn(1.6f); // Client
                cols.RelativeColumn(1.3f); // Payroll Date
                cols.RelativeColumn(0.8f); // Basic Hrs
                cols.RelativeColumn(1);    // Basic
                cols.RelativeColumn(0.8f); // OT Hrs
                cols.RelativeColumn(1);    // OT
                cols.RelativeColumn(0.8f); // ND Hrs
                cols.RelativeColumn(1);    // ND
                cols.RelativeColumn(0.8f); // Holiday Hrs
                cols.RelativeColumn(1);    // Holiday
                cols.RelativeColumn(1);    // Allowances
                cols.RelativeColumn(1);    // Other Income
                cols.RelativeColumn(1);    // One-Time Payout (Company)
                cols.RelativeColumn(1);    // One-Time Payout (Government)
                cols.RelativeColumn(1.1f); // Gross
                cols.RelativeColumn(1);    // SSS
                cols.RelativeColumn(1);    // PhilHealth
                cols.RelativeColumn(1);    // Pag-IBIG
                cols.RelativeColumn(1);    // W-Tax
                cols.RelativeColumn(1);    // Loans
                cols.RelativeColumn(1);    // Other Ded
                cols.RelativeColumn(1.2f); // Net Pay
            });

            static IContainer HeaderCell(IContainer c) =>
                c.Background(TableHeaderBg).Padding(3);
            static IContainer DataCell(IContainer c) =>
                c.BorderBottom(1).BorderColor("#eeeeee").Padding(3);

            table.Header(header =>
            {
                header.Cell().Element(HeaderCell).Text("Employee").Bold();
                header.Cell().Element(HeaderCell).Text("Client").Bold();
                header.Cell().Element(HeaderCell).Text("Payroll Date").Bold();
                header.Cell().Element(HeaderCell).AlignRight().Text("Basic Hrs").Bold();
                header.Cell().Element(HeaderCell).AlignRight().Text("Basic").Bold();
                header.Cell().Element(HeaderCell).AlignRight().Text("OT Hrs").Bold();
                header.Cell().Element(HeaderCell).AlignRight().Text("OT").Bold();
                header.Cell().Element(HeaderCell).AlignRight().Text("ND Hrs").Bold();
                header.Cell().Element(HeaderCell).AlignRight().Text("ND").Bold();
                header.Cell().Element(HeaderCell).AlignRight().Text("Holiday Hrs").Bold();
                header.Cell().Element(HeaderCell).AlignRight().Text("Holiday").Bold();
                header.Cell().Element(HeaderCell).AlignRight().Text("Allow.").Bold();
                header.Cell().Element(HeaderCell).AlignRight().Text("Other Inc.").Bold();
                header.Cell().Element(HeaderCell).AlignRight().Text("1x Payout (Co)").Bold();
                header.Cell().Element(HeaderCell).AlignRight().Text("1x Payout (Gov)").Bold();
                header.Cell().Element(HeaderCell).AlignRight().Text("Gross").Bold();
                header.Cell().Element(HeaderCell).AlignRight().Text("SSS").Bold();
                header.Cell().Element(HeaderCell).AlignRight().Text("PhilHealth").Bold();
                header.Cell().Element(HeaderCell).AlignRight().Text("Pag-IBIG").Bold();
                header.Cell().Element(HeaderCell).AlignRight().Text("W-Tax").Bold();
                header.Cell().Element(HeaderCell).AlignRight().Text("Loans").Bold();
                header.Cell().Element(HeaderCell).AlignRight().Text("Other Ded.").Bold();
                header.Cell().Element(HeaderCell).AlignRight().Text("Net Pay").Bold();
            });

            foreach (var r in _rows)
            {
                table.Cell().Element(DataCell).Text(r.FullName);
                table.Cell().Element(DataCell).Text(ClientNameFor(r)).FontColor(LabelColor);
                table.Cell().Element(DataCell).Text(r.PayrollDate.ToString("MMM dd, yyyy")).FontColor(LabelColor);
                table.Cell().Element(DataCell).AlignRight().Text(Hours(BasicHours(r))).FontColor(LabelColor);
                table.Cell().Element(DataCell).AlignRight().Text(Money(BasicPay(r)));
                table.Cell().Element(DataCell).AlignRight().Text(Hours(OtHours(r))).FontColor(LabelColor);
                table.Cell().Element(DataCell).AlignRight().Text(Money(OtPay(r)));
                table.Cell().Element(DataCell).AlignRight().Text(Hours(NdHours(r))).FontColor(LabelColor);
                table.Cell().Element(DataCell).AlignRight().Text(Money(NdPay(r)));
                table.Cell().Element(DataCell).AlignRight().Text(Hours(HolidayHours(r))).FontColor(LabelColor);
                table.Cell().Element(DataCell).AlignRight().Text(Money(HolidayPay(r)));
                table.Cell().Element(DataCell).AlignRight().Text(Money(r.TotalRegularAllowances));
                table.Cell().Element(DataCell).AlignRight().Text(Money(r.TotalOtherIncome));
                table.Cell().Element(DataCell).AlignRight().Text(Money(r.CompanyFundedLeavePay));
                table.Cell().Element(DataCell).AlignRight().Text(Money(r.GovernmentFundedLeavePay));
                table.Cell().Element(DataCell).AlignRight().Text(Money(r.GrossIncome)).Bold();
                table.Cell().Element(DataCell).AlignRight().Text(Money(r.SSSContribution));
                table.Cell().Element(DataCell).AlignRight().Text(Money(r.PhilHealthContribution));
                table.Cell().Element(DataCell).AlignRight().Text(Money(r.PagIbigContribution));
                table.Cell().Element(DataCell).AlignRight().Text(Money(r.WithholdingTax));
                table.Cell().Element(DataCell).AlignRight().Text(Money(r.TotalLoans));
                table.Cell().Element(DataCell).AlignRight().Text(Money(r.OtherDeductions - r.TotalLoans));
                table.Cell().Element(DataCell).AlignRight().Text(Money(r.NetPay)).Bold().FontColor(Primary);
            }

            static IContainer TotalCell(IContainer c) =>
                c.BorderTop(1).BorderColor(BorderColor).Padding(3);

            table.Cell().Element(TotalCell).Text("TOTAL").Bold();
            table.Cell().Element(TotalCell).Text("");
            table.Cell().Element(TotalCell).Text("");
            table.Cell().Element(TotalCell).AlignRight().Text(Hours(_rows.Sum(BasicHours))).Bold();
            table.Cell().Element(TotalCell).AlignRight().Text(Money(_rows.Sum(BasicPay))).Bold();
            table.Cell().Element(TotalCell).AlignRight().Text(Hours(_rows.Sum(OtHours))).Bold();
            table.Cell().Element(TotalCell).AlignRight().Text(Money(_rows.Sum(OtPay))).Bold();
            table.Cell().Element(TotalCell).AlignRight().Text(Hours(_rows.Sum(NdHours))).Bold();
            table.Cell().Element(TotalCell).AlignRight().Text(Money(_rows.Sum(NdPay))).Bold();
            table.Cell().Element(TotalCell).AlignRight().Text(Hours(_rows.Sum(HolidayHours))).Bold();
            table.Cell().Element(TotalCell).AlignRight().Text(Money(_rows.Sum(HolidayPay))).Bold();
            table.Cell().Element(TotalCell).AlignRight().Text(Money(_rows.Sum(r => r.TotalRegularAllowances))).Bold();
            table.Cell().Element(TotalCell).AlignRight().Text(Money(_rows.Sum(r => r.TotalOtherIncome))).Bold();
            table.Cell().Element(TotalCell).AlignRight().Text(Money(_rows.Sum(r => r.CompanyFundedLeavePay))).Bold();
            table.Cell().Element(TotalCell).AlignRight().Text(Money(_rows.Sum(r => r.GovernmentFundedLeavePay))).Bold();
            table.Cell().Element(TotalCell).AlignRight().Text(Money(_rows.Sum(r => r.GrossIncome))).Bold();
            table.Cell().Element(TotalCell).AlignRight().Text(Money(_rows.Sum(r => r.SSSContribution))).Bold();
            table.Cell().Element(TotalCell).AlignRight().Text(Money(_rows.Sum(r => r.PhilHealthContribution))).Bold();
            table.Cell().Element(TotalCell).AlignRight().Text(Money(_rows.Sum(r => r.PagIbigContribution))).Bold();
            table.Cell().Element(TotalCell).AlignRight().Text(Money(_rows.Sum(r => r.WithholdingTax))).Bold();
            table.Cell().Element(TotalCell).AlignRight().Text(Money(_rows.Sum(r => r.TotalLoans))).Bold();
            table.Cell().Element(TotalCell).AlignRight().Text(Money(_rows.Sum(r => r.OtherDeductions - r.TotalLoans))).Bold();
            table.Cell().Element(TotalCell).AlignRight().Text(Money(_rows.Sum(r => r.NetPay))).Bold().FontColor(Primary);
        });
    }
}
