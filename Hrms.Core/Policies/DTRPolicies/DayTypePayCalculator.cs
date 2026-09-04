namespace Hrms.Core.Policies.DTRPolicies;

/// <summary>
/// GoF Template Method, parameterized rather than subclassed — the six day-type premium
/// calculators this replaces (Regular/Special Holiday, their Rest-Day combos, Double Legal
/// Holiday and its Rest-Day combo) never varied the pricing algorithm, only the numbers fed
/// into it, so one class owns the algorithm and each call site supplies its own inputs:
///   - Ineligible: 1.0x the base hourly rate.
///   - Eligible, base pay pre-funded (Fixed-salary employee with the matching Fixed Salary
///     Inclusion toggle on): only the delta premium above the 1.0x base already paid
///     (e.g. 2.0x total - 1.0x base = 1.0x).
///   - Eligible, not pre-funded (Daily-paid, or the toggle is off): the full multiplier.
/// "No work, no pay" day types (Special Non-Working Holiday and its Rest-Day combo) pass
/// unworkedMultiplier: 0m, which zeroes out CalculateUnworkedPay regardless of eligibility —
/// no separate branch needed for that DOLE rule.
/// Passing a <paramref name="line"/> mirrors each stage's own amount (not a running total)
/// onto its Worked/UnWork fields, for the day types BasicPayrollModel reports separately.
/// </summary>
internal sealed class DayTypePayCalculator
{
    private readonly bool _isEligible;
    private readonly bool _isBasePayPreFunded;
    private readonly decimal _workedMultiplier;
    private readonly decimal _unworkedMultiplier;
    private readonly BasicPipelineData? _line;

    public decimal WorkedAmount { get; private set; }
    public decimal UnworkedAmount { get; private set; }
    public decimal Total => WorkedAmount + UnworkedAmount;

    private DayTypePayCalculator(
        bool isEligible,
        bool isBasePayPreFunded,
        decimal workedMultiplier,
        decimal unworkedMultiplier,
        BasicPipelineData? line)
    {
        _isEligible = isEligible;
        _isBasePayPreFunded = isBasePayPreFunded;
        _workedMultiplier = workedMultiplier;
        _unworkedMultiplier = unworkedMultiplier;
        _line = line;
    }

    /// <param name="workedMultiplier">The full worked-day rate multiplier when not pre-funded (e.g. 2.0 for a regular holiday).</param>
    /// <param name="unworkedMultiplier">The full unworked-day rate multiplier when not pre-funded, or 0m for "no work, no pay" day types.</param>
    /// <param name="line">Optional — when given, each stage's amount is mirrored onto its Worked/UnWork fields.</param>
    public static DayTypePayCalculator For(
        bool isEligible,
        bool isBasePayPreFunded,
        decimal workedMultiplier,
        decimal unworkedMultiplier,
        BasicPipelineData? line = null) =>
        new(isEligible, isBasePayPreFunded, workedMultiplier, unworkedMultiplier, line);

    public DayTypePayCalculator CalculateWorkedPay(decimal hourlyRate, decimal workedHours)
    {
        if (workedHours <= 0) return this;

        var multiplier = !_isEligible
            ? 1.0m
            : (_isBasePayPreFunded ? Math.Max(0m, _workedMultiplier - 1.0m) : _workedMultiplier);

        WorkedAmount = hourlyRate * workedHours * multiplier;
        if (_line != null) _line.Worked = WorkedAmount;
        return this;
    }

    public DayTypePayCalculator CalculateUnworkedPay(decimal hourlyRate, decimal unworkedHours)
    {
        if (unworkedHours <= 0 || !_isEligible || _unworkedMultiplier <= 0m) return this;

        var multiplier = _isBasePayPreFunded ? Math.Max(0m, _unworkedMultiplier - 1.0m) : _unworkedMultiplier;

        UnworkedAmount = hourlyRate * unworkedHours * multiplier;
        if (_line != null) _line.UnWork = UnworkedAmount;
        return this;
    }
}
