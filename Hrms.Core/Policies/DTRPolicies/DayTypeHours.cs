using Hrms.Core.Calculators.DtrPay;

namespace Hrms.Core.Policies.DTRPolicies;

/// <summary>
/// The worked hours one day-type policy pays at its day rate, by OT/ND calculation method.
/// The DTR stores natural hours (dtr-api DailyRecordBuilder): Hrs includes its night portion,
/// ND holds every night hour (regular + OT) and NDOT is the night-OT subset of both. So:
///   Plain = Hrs - (ND - NDOT)   the daytime hours
///   Night = ND - NDOT           the night hours inside Hrs
/// - Additive: the policy pays the natural Hrs (Plain + Night) at its day rate; the ND bucket
///   then adds only the night premium (AdditiveDayTypePayBase).
/// - Compounded: the policy pays Plain only; the night hours are priced by the ND bucket at
///   day x ND (CompoundedDayTypePayBase).
/// Plain keeps each policy's own rules (holiday eligibility, Fixed-salary pre-funding, unworked
/// hours = shift - Plain). Night is paid at the category's full day rate, never pre-funded,
/// and only for night-differential-eligible employees -- exactly how those hours were paid
/// before the DTR stored natural hours.
/// </summary>
internal readonly record struct DayTypeHours(decimal Plain, decimal Night)
{
    public static DayTypeHours For(PayrollContext context, double hrs, double nd, double ndot)
    {
        var night = Math.Max(0m, (decimal)nd - (decimal)ndot);
        var plain = Math.Max(0m, (decimal)hrs - night);
        return new DayTypeHours(plain, PaysNightAtDayRate(context) ? night : 0m);
    }

    // Additive only: adds the night hours at the category's full day rate to the day pay.
    // Night is already 0 under Compounded (and when not ND-eligible), so this is a no-op there.
    public BasicPipelineData AddNightPay(BasicPipelineData line, PayrollContext context, PayCategory category)
    {
        if (Night <= 0m) return line;

        // Same operation order the ND-base pricer used (hours x hourly, then x day rate).
        var pay = Night * RateHelper.GetHourlyRate(context) * category.OtRates.ResolveDayRate(context);
        line.Value += pay;
        line.Worked += pay;
        return line;
    }

    // Same gates the ND bucket applies (EligibleHoursFilter): no hourly rate without a shift,
    // and no night pay for an employee who isn't night-differential eligible.
    private static bool PaysNightAtDayRate(PayrollContext context) =>
        context.OtNdCalculationMethod == OtNdCalculationMethod.Additive
        && context.DailyRecord.ShiftWorkingHour > 0
        && new IsEligibleForNightDifferential().IsSatisfiedBy(context);
}
