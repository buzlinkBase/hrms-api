using Hrms.Domain.Entities;

namespace Hrms.Api.Documents.Fixed;

internal enum FixedPayslipRowKind
{
    Header,     // shaded sub-section label, no amount
    Earning,    // adds to the section
    Cut,        // taken off Basic Salary -- rendered as a negative amount
    Subtotal,   // bold ruled-off figure (Net Basic Pay)
    Note,       // informational, already included in a figure above -- rendered muted
}

// Made internal (not private), matching PayslipHoursDocument's EarningsRow convention, so the row
// math is directly testable without rendering a PDF -- see hrms.test's InternalsVisibleTo.
internal readonly record struct FixedPayslipRow(string Label, decimal Amount, FixedPayslipRowKind Kind);

// Builder for the Earnings box of the Fixed-salary payslip. A Fixed employee is paid a flat period
// share of the monthly rate, not hours x rate, so the layout is:
//   Basic Salary - late/under-time/absences/unpaid leave = Net Basic Pay (== Payroll.BasicPay)
//   + holiday / rest-day lines (labelled "Premium" when the monthly rate already covers the day,
//     "Pay" when it doesn't -- see the Is*Included snapshot flags on Payroll)
//   + overtime / night differential + other income = Gross Income.
// Zero-amount lines are omitted. Every figure comes straight off the saved Payroll row.
internal sealed class FixedPayslipRowBuilder
{
    // Below this a difference is rounding noise, not a real adjustment worth its own line.
    private const decimal Tolerance = 0.005m;

    private readonly Payroll _p;
    private readonly List<FixedPayslipRow> _rows = new();

    public FixedPayslipRowBuilder(Payroll payroll) => _p = payroll;

    public IReadOnlyList<FixedPayslipRow> Build()
    {
        _rows.Clear();
        AddBasicSection();
        AddHolidaySection();
        AddRestDaySection();
        AddOvertimeAndNightDifferentialSection();
        AddOtherIncomeSection();
        return _rows.ToList();
    }

    // Basic Salary as shown: the snapshot taken at generation time, or -- for a row generated
    // before FixedBasicSalary existed -- rebuilt by adding the attendance cuts back onto BasicPay.
    internal decimal BasicSalary =>
        _p.FixedBasicSalary > 0m
            ? _p.FixedBasicSalary
            : _p.BasicPay + AttendanceCuts;

    private decimal AttendanceCuts => _p.LateAmount + _p.UnderTimeAmount + _p.AbsencesAmount + _p.UnpaidLeaves;

    private void AddBasicSection()
    {
        Add("Basic Salary", BasicSalary, FixedPayslipRowKind.Earning);
        Add("Late", _p.LateAmount, FixedPayslipRowKind.Cut);
        Add("Under Time", _p.UnderTimeAmount, FixedPayslipRowKind.Cut);
        Add("Absences", _p.AbsencesAmount, FixedPayslipRowKind.Cut);
        Add("Unpaid Leave", _p.UnpaidLeaves, FixedPayslipRowKind.Cut);

        // Whatever still separates (Basic - cuts) from the saved BasicPay: a one-time leave payout
        // (its calendar days are cut from the flat rate and paid as a lump sum instead), a salary
        // adjustment added to BasicPay after the fact, or BasicPay floored at zero. Shown so the
        // lines always add up to Net Basic Pay.
        var residual = BasicSalary - AttendanceCuts - _p.BasicPay;
        if (residual > Tolerance)
        {
            var label = string.IsNullOrWhiteSpace(_p.OneTimePayoutBreakdown)
                ? "Other Basic Pay Adjustment"
                : "One-Time Leave (paid as lump sum)";
            _rows.Add(new FixedPayslipRow(label, residual, FixedPayslipRowKind.Cut));
        }
        else if (residual < -Tolerance)
        {
            _rows.Add(new FixedPayslipRow("Salary Adjustment", -residual, FixedPayslipRowKind.Earning));
        }

        _rows.Add(new FixedPayslipRow("Net Basic Pay", _p.BasicPay, FixedPayslipRowKind.Subtotal));
        // Paid leave is covered by the flat rate, so it's information, not an extra earning.
        Add("Paid Leave (included in Net Basic Pay)", _p.PaidLeaves, FixedPayslipRowKind.Note);
    }

    // Holiday lines carry only the day-type pay itself -- the OT/ND worked on a holiday sit in the
    // overtime and night differential section so nothing is counted twice.
    private void AddHolidaySection()
    {
        var restAndRegular = _p.IsRestDayPaid && _p.IsRegularHolidayIncluded;
        var restAndSpecial = _p.IsRestDayPaid && _p.IsSpecialNonWorkingIncluded;

        var start = _rows.Count;
        AddWorkedAndUnworked("Legal Holiday", _p.LegalPay, _p.LegalHolidayUnworkedPay, _p.IsRegularHolidayIncluded);
        AddWorkedAndUnworked("Rest Day + Legal Holiday", _p.RestLegalPay, _p.RestLegalUnworkedPay, restAndRegular);
        AddWorkedAndUnworked("Double Legal Holiday", _p.DoubleLegalPay, _p.DoubleLegalUnworkedPay, _p.IsRegularHolidayIncluded);
        AddWorkedAndUnworked("Rest Day + Double Legal Holiday", _p.RestDoubleLegalPay, _p.RestDoubleLegalUnworkedPay, restAndRegular);
        Add(PayOrPremium("Special Holiday", _p.IsSpecialNonWorkingIncluded), _p.SpecialPay, FixedPayslipRowKind.Earning);
        Add(PayOrPremium("Rest Day + Special Holiday", restAndSpecial), _p.RestSpecialPay, FixedPayslipRowKind.Earning);

        InsertHeaderIfAnyRowsAdded(start, "HOLIDAY");
    }

    private void AddRestDaySection()
    {
        var start = _rows.Count;
        Add(PayOrPremium("Rest Day", _p.IsRestDayPaid), _p.RestDayPay, FixedPayslipRowKind.Earning);
        InsertHeaderIfAnyRowsAdded(start, "REST DAY");
    }

    private void AddOvertimeAndNightDifferentialSection()
    {
        var start = _rows.Count;
        Add("Overtime", _p.OvertimePay, FixedPayslipRowKind.Earning);
        Add("Night Differential", _p.NightDifferentialPay, FixedPayslipRowKind.Earning);
        Add("Night Differential Overtime", _p.NightDifferentialOTPay, FixedPayslipRowKind.Earning);
        InsertHeaderIfAnyRowsAdded(start, "OVERTIME & NIGHT DIFFERENTIAL");
    }

    private void AddOtherIncomeSection()
    {
        var start = _rows.Count;
        Add("One-Time Leave Payout (Company)", _p.CompanyFundedLeavePay, FixedPayslipRowKind.Earning);
        Add("COLA", _p.Cola, FixedPayslipRowKind.Earning);
        Add("Regular Allowances", _p.TotalRegularAllowances, FixedPayslipRowKind.Earning);
        Add("Bonuses", _p.TotalBonuses, FixedPayslipRowKind.Earning);
        Add("Commissions", _p.TotalCommissions, FixedPayslipRowKind.Earning);
        Add("De Minimis", _p.TotalDeminimises, FixedPayslipRowKind.Earning);
        Add("Reimbursement", _p.Reimbursement, FixedPayslipRowKind.Earning);
        Add("Other Income", _p.TotalOtherIncome, FixedPayslipRowKind.Earning);
        InsertHeaderIfAnyRowsAdded(start, "OTHER INCOME");
    }

    // {Category}Pay is worked + unworked blended; the unworked slice (only legal-holiday-involving
    // days ever have one) is split out so each line is one thing. When the holiday is already in
    // the monthly rate the unworked slice is never paid, so that line simply doesn't appear.
    private void AddWorkedAndUnworked(string name, decimal blendedPay, decimal unworkedPay, bool includedInRate)
    {
        Add($"{PayOrPremium(name, includedInRate)} (Worked)", blendedPay - unworkedPay, FixedPayslipRowKind.Earning);
        Add($"{name} Pay (Unworked)", unworkedPay, FixedPayslipRowKind.Earning);
    }

    private static string PayOrPremium(string name, bool includedInRate) =>
        includedInRate ? $"{name} Premium" : $"{name} Pay";

    private void Add(string label, decimal amount, FixedPayslipRowKind kind)
    {
        if (amount == 0m) return;
        _rows.Add(new FixedPayslipRow(label, amount, kind));
    }

    private void InsertHeaderIfAnyRowsAdded(int startIndex, string title)
    {
        if (_rows.Count == startIndex) return;
        _rows.Insert(startIndex, new FixedPayslipRow(title, 0m, FixedPayslipRowKind.Header));
    }
}
