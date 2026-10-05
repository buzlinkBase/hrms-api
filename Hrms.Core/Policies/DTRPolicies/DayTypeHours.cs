namespace Hrms.Core.Policies.DTRPolicies;

/// <summary>
/// The worked hours one day-type policy pays at its day rate, by OT/ND calculation method.
/// The DTR stores natural hours (dtr-api DailyRecordBuilder): Hrs includes its night portion,
/// ND holds every night hour (regular + OT) and NDOT is the night-OT subset of both. So:
///   Plain = Hrs - (ND - NDOT)   the daytime hours
/// - Additive: the policy pays the natural Hrs (night hours included) at its day rate; the ND
///   bucket then adds only the night premium (AdditiveDayTypePayBase).
/// - Compounded: the policy pays Plain only; the night hours are priced by the ND bucket at
///   day x ND (CompoundedDayTypePayBase).
/// Whichever hours are paid keep each policy's own rules (holiday eligibility, Fixed-salary
/// pre-funding, unworked hours = shift - paid hours).
/// </summary>
internal readonly record struct DayTypeHours(decimal Plain)
{
    public static DayTypeHours For(PayrollContext context, double hrs, double nd, double ndot)
    {
        var night = Math.Max(0m, (decimal)nd - (decimal)ndot);
        return new DayTypeHours(Math.Max(0m, (decimal)hrs - night));
    }

    // The hours the day-type policy pays at its own rate under the context's pay method.
    public decimal PaidHours(PayrollContext context, double naturalHours) =>
        context.OtNdCalculationMethod == OtNdCalculationMethod.Additive ? (decimal)naturalHours : Plain;
}
