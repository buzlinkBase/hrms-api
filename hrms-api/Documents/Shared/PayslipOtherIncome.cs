using Hrms.Domain.Entities;
using Hrms.Domain.ValueObjects;

namespace Hrms.Api.Documents.Shared;

// The Other Income rows every payslip layout prints (between COLA and Gross Income) -- one place
// deciding them, so the Standard, Hours and Fixed layouts can't drift apart.
//
// Itemized by income name ("HG Allowance") from the schedule rows this payroll consumed (see
// IncomeAplDtlService.GetPayslipLinesAsync) when those add up to the payroll's own schedule-fed
// totals. Otherwise -- a payroll saved before rows were stamped with the payroll that paid them,
// or any mismatch -- it falls back to the category totals saved on the Payroll row, so a payslip
// always adds up to its Gross Income. COLA and the one-time leave payout aren't schedule rows,
// so they stay where each layout already prints them.
public sealed class PayslipOtherIncome
{
    private const decimal Tolerance = 0.005m;

    public static readonly PayslipOtherIncome Categories = new([]);

    private readonly IReadOnlyList<PayslipIncomeLine> _items;

    public PayslipOtherIncome(IReadOnlyList<PayslipIncomeLine> items) => _items = items;

    internal static decimal ScheduledTotal(Payroll p) =>
        p.TotalRegularAllowances + p.TotalBonuses + p.TotalCommissions +
        p.TotalDeminimises + p.Reimbursement + p.TotalOtherIncome;

    public bool IsItemized(Payroll p) =>
        _items.Count > 0 && Math.Abs(_items.Sum(x => x.Amount) - ScheduledTotal(p)) < Tolerance;

    // Itemized lines leave zero amounts out. The category fallback keeps every category, and
    // each layout's own row helper decides whether a zero is printed (Standard prints it, as before).
    public IReadOnlyList<PayslipIncomeLine> Rows(Payroll p)
    {
        if (IsItemized(p))
            return _items.Where(x => x.Amount != 0m).ToList();

        return
        [
            new("Regular Allowances", p.TotalRegularAllowances),
            new("Bonuses", p.TotalBonuses),
            new("Commissions", p.TotalCommissions),
            new("De Minimis", p.TotalDeminimises),
            new("Reimbursement", p.Reimbursement),
            new("Other Income", p.TotalOtherIncome),
        ];
    }
}
