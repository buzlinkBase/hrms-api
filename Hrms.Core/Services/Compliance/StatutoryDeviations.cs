using System.Globalization;
using Hrms.Domain;

namespace Hrms.Core.Services.Compliance;

// One setting whose value differs from the Philippine statutory (DOLE) default. The company may
// choose it -- the system supports it on purpose -- but an admin must acknowledge it once
// (ComplianceAcknowledgmentService), and the Terms of Service put the responsibility on the company.
public sealed record StatutoryDeviation(string Key, string Label, string StatutoryValue, string ConfiguredValue, string Note);

// What one save would set: the company policy, a set of rate multipliers, or a client's statutory caps.
public sealed record CompanyPolicySubject(OtNdCalculationMethod OtNdCalculationMethod);
public sealed record RateSubject(IReadOnlyDictionary<RateType, decimal> Rates);
public sealed record StatutoryCapSubject(decimal? MaxSss, decimal? MaxPhilHealth, decimal? MaxPagIbig);

// Strategy: one rule per kind of deviation. A new rule is one more class in StatutoryDeviationRules,
// with no branching in the services or controllers that save the settings.
public interface IStatutoryDeviationRule<in TSubject>
{
    IEnumerable<StatutoryDeviation> Evaluate(TSubject subject);
}

public sealed class AdditiveOtNdMethodRule : IStatutoryDeviationRule<CompanyPolicySubject>
{
    public IEnumerable<StatutoryDeviation> Evaluate(CompanyPolicySubject subject)
    {
        if (subject.OtNdCalculationMethod != OtNdCalculationMethod.Additive) yield break;
        yield return new StatutoryDeviation(
            "policy.ot-nd-method",
            "OT/ND calculation method",
            nameof(OtNdCalculationMethod.Compounded),
            nameof(OtNdCalculationMethod.Additive),
            "Additive pays less than the DOLE-standard Compounded method on rest days and holidays.");
    }
}

public sealed class RateBelowStatutoryFloorRule : IStatutoryDeviationRule<RateSubject>
{
    // The least each premium may be under the Labor Code and DOLE's holiday/premium pay rules,
    // as a multiplier of the basic hourly rate.
    public static readonly IReadOnlyDictionary<RateType, decimal> Floors = new Dictionary<RateType, decimal>
    {
        [RateType.REGULAR] = 1.00m,
        [RateType.NIGHTDIFF] = 1.10m,
        [RateType.OVERTIME] = 1.25m,
        [RateType.RESTDAY_DUTY] = 1.30m,
        [RateType.LEGAL_HOLIDAY] = 1.00m,
        [RateType.LEGAL_HOLIDAY_DUTY] = 2.00m,
        [RateType.SPECIAL_WORKING] = 1.00m,
        [RateType.SPECIAL_NON_WORKING] = 1.30m,
        [RateType.RESTDAY_SPECIAL] = 1.50m,
        [RateType.HOLIDAY_OT] = 1.30m,
        [RateType.RESTDAY_OT_PREMIUM] = 1.30m,
        [RateType.LEGAL_HOLIDAY_OT_PREMIUM] = 1.30m,
        [RateType.SPECIAL_HOLIDAY_OT_PREMIUM] = 1.30m,
        [RateType.RESTLEGAL_OT_PREMIUM] = 1.30m,
        [RateType.RESTSPECIAL_OT_PREMIUM] = 1.30m,
        [RateType.DOUBLELEGAL_OT_PREMIUM] = 1.30m,
        [RateType.RESTDOUBLELEGAL_OT_PREMIUM] = 1.30m,
    };

    public IEnumerable<StatutoryDeviation> Evaluate(RateSubject subject)
    {
        foreach (var (type, rate) in subject.Rates.OrderBy(r => r.Key))
        {
            if (!Floors.TryGetValue(type, out var floor) || rate >= floor) continue;
            yield return new StatutoryDeviation(
                $"rate.{type}",
                $"{type} rate multiplier",
                Format(floor),
                Format(rate),
                "Below the DOLE minimum premium for this kind of work.");
        }
    }

    private static string Format(decimal value) => value.ToString("0.00##", CultureInfo.InvariantCulture);
}

public sealed class StatutoryCapRule : IStatutoryDeviationRule<StatutoryCapSubject>
{
    public IEnumerable<StatutoryDeviation> Evaluate(StatutoryCapSubject subject)
    {
        var caps = new (string Key, string Label, decimal? Value)[]
        {
            ("cap.sss", "SSS employee share cap", subject.MaxSss),
            ("cap.philhealth", "PhilHealth employee share cap", subject.MaxPhilHealth),
            ("cap.pagibig", "Pag-IBIG employee share cap", subject.MaxPagIbig),
        };
        foreach (var (key, label, value) in caps)
        {
            if (value is not > 0) continue;
            yield return new StatutoryDeviation(
                key, label, "No cap (per the contribution table)",
                value.Value.ToString("0.00", CultureInfo.InvariantCulture),
                "The employee share deducted is capped below what the contribution table requires.");
        }
    }
}

// Composite: every rule for a subject, run together.
public static class StatutoryDeviationRules
{
    private static readonly IStatutoryDeviationRule<CompanyPolicySubject>[] Policy = [new AdditiveOtNdMethodRule()];
    private static readonly IStatutoryDeviationRule<RateSubject>[] Rates = [new RateBelowStatutoryFloorRule()];
    private static readonly IStatutoryDeviationRule<StatutoryCapSubject>[] Caps = [new StatutoryCapRule()];

    public static IReadOnlyList<StatutoryDeviation> Evaluate(CompanyPolicySubject subject) => Run(Policy, subject);
    public static IReadOnlyList<StatutoryDeviation> Evaluate(RateSubject subject) => Run(Rates, subject);
    public static IReadOnlyList<StatutoryDeviation> Evaluate(StatutoryCapSubject subject) => Run(Caps, subject);

    private static IReadOnlyList<StatutoryDeviation> Run<T>(IEnumerable<IStatutoryDeviationRule<T>> rules, T subject) =>
        rules.SelectMany(r => r.Evaluate(subject)).ToList();
}

// The save was refused because it brings in deviations nobody has acknowledged yet. 409: the
// frontend shows them, and on confirmation sends the same request again with acknowledgeDeviations=true.
public sealed class DeviationAcknowledgmentRequiredException(IReadOnlyList<StatutoryDeviation> deviations)
    : Exception("This change differs from the statutory (DOLE) defaults and must be acknowledged.")
{
    public const string ProblemType = "statutory-deviation-ack-required";
    public IReadOnlyList<StatutoryDeviation> Deviations { get; } = deviations;
}
