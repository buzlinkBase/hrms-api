namespace Hrms.Core.Calculators.DtrPay;

// Zeroes out the hours an employee isn't eligible to be paid for, before either pay method sees
// them -- one rule set for both methods instead of a copy inside every OT/ND/NDOT policy.
internal interface IEligibleHoursFilter
{
    CategoryHours Filter(CategoryHours hours, PayrollContext context);
}

// Same gates the old policies applied, bucket by bucket:
// - OT hours need overtime eligibility (IsEligibleForOvertime).
// - ND and NDOT hours need night-differential eligibility (IsEligibleForNightDifferential) --
//   NDOT was never gated on overtime eligibility, and still isn't.
// - Nothing premium is paid without a shift length (the hourly rate divides by it).
// - Negative hours count as none.
// Plain Hrs pass through untouched: the day-type policies apply their own rules to them.
internal sealed class EligibleHoursFilter : IEligibleHoursFilter
{
    private readonly IPayrollSpec<PayrollContext> _overtime;
    private readonly IPayrollSpec<PayrollContext> _nightDiff;

    public EligibleHoursFilter()
        : this(new IsEligibleForOvertime(), new IsEligibleForNightDifferential()) { }

    internal EligibleHoursFilter(IPayrollSpec<PayrollContext> overtime, IPayrollSpec<PayrollContext> nightDiff)
    {
        _overtime = overtime;
        _nightDiff = nightDiff;
    }

    public CategoryHours Filter(CategoryHours hours, PayrollContext context)
    {
        if (context.DailyRecord.ShiftWorkingHour <= 0)
            return hours with { Ot = 0m, Nd = 0m, Ndot = 0m };

        var nightDiff = _nightDiff.IsSatisfiedBy(context);
        return hours with
        {
            Ot = _overtime.IsSatisfiedBy(context) ? Math.Max(0m, hours.Ot) : 0m,
            Nd = nightDiff ? Math.Max(0m, hours.Nd) : 0m,
            Ndot = nightDiff ? Math.Max(0m, hours.Ndot) : 0m,
        };
    }
}

// Resolves one category's rates for one day -- the single place both pay methods get them from.
internal interface ICategoryRateResolver
{
    CategoryRates Resolve(PayCategory category, PayrollContext context);
}

internal sealed class CategoryRateResolver : ICategoryRateResolver
{
    public CategoryRates Resolve(PayCategory category, PayrollContext context)
    {
        var (dayRate, _) = category.OtRates.Resolve(context);
        return new CategoryRates(
            Hourly: PremiumRateHelper.GetHourlyRate(context),
            Day: dayRate,
            RawOt: category.OtRates.ResolveRawOtRate(context),
            Nd: PremiumRateHelper.GetRate(context, RateType.NIGHTDIFF, RATE_DEFAULT.NIGHTDIFF));
    }
}

// Adapter: turns a category's saved DTR hours into the shape a pay method prices.
internal interface IHoursShaper
{
    CategoryHours Shape(CategoryHours saved);
}

// Compounded: the hours exactly as the DTR saved them -- Hrs, OT, ND and NDOT each on their own.
internal sealed class SavedHoursShaper : IHoursShaper
{
    public CategoryHours Shape(CategoryHours saved) => saved;
}

// Additive: the DTR screens' display hours -- the C# twin of the UI's toDisplayHours
// (one-punch-hris-ui src/app/modules/daily-time-record/detail/utils/dtr-hours-display.ts):
//   Hrs = Hrs + ND,  OT = OT + NDOT,  ND = ND + NDOT,  NDOT = 0.
// Hrs + ND is all regular-rate hours, OT + NDOT all overtime, ND + NDOT all night hours, so
// pricing these buckets separately and adding them up is exactly what "additive" means.
internal sealed class DisplayHoursShaper : IHoursShaper
{
    public CategoryHours Shape(CategoryHours saved) => new(
        Hrs: saved.Hrs + saved.Nd,
        Ot: saved.Ot + saved.Ndot,
        Nd: saved.Nd + saved.Ndot,
        Ndot: 0m);
}
