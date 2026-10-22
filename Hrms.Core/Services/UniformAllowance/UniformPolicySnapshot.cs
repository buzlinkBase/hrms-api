using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;

namespace Hrms.Core.Services.UniformAllowance;

/// <summary>
/// The uniform allowance rules in force for a client on one day -- a saved policy version, or the
/// client's legacy Client.UniformAllowance/UniformAllowanceBasis settings before it has any.
/// Everything that decides, projects or describes a credit reads this, never the entity.
/// </summary>
public sealed record UniformPolicySnapshot(
    Guid? PolicyId,
    DateOnly EffectiveFrom,
    bool IsEnabled,
    UniformCreditFrequency Frequency,
    int CreditDay,
    int StartMonth,
    UniformAmountMode AmountMode,
    decimal Amount,
    int? ExpectedDaysPerPeriod,
    IReadOnlyList<EmploymentStatus> EligibleStatuses,
    int WaitingMonths,
    int? MinPresentDays,
    decimal? MaxBalance,
    UniformBalanceExpiry Expiry,
    int? ExpiryMonth,
    int? ExpiryAfterMonths,
    UniformSeparationRule OnSeparation,
    UniformRehireRule RehireRule,
    int? RehireBridgeDays)
{
    /// <summary>The legacy columns' Present-Days rule: this many present days in the month.</summary>
    public const int LegacyPresentDaysThreshold = 20;

    public static readonly IReadOnlyList<EmploymentStatus> LegacyStatuses =
        [EmploymentStatus.Regular, EmploymentStatus.Probationary, EmploymentStatus.Contract];

    public bool IsLegacy => PolicyId == null;

    /// <summary>Whether deciding a credit needs the period's present-day count.</summary>
    public bool NeedsAttendance =>
        MinPresentDays != null || AmountMode != UniformAmountMode.FixedPerPeriod;

    public static UniformPolicySnapshot From(UniformAllowancePolicy p) => new(
        p.Id, p.EffectiveFrom, p.IsEnabled, p.Frequency, p.CreditDay, p.StartMonth,
        p.AmountMode, p.Amount, p.ExpectedDaysPerPeriod, p.EligibleStatuses.ToList(),
        p.WaitingMonths, p.MinPresentDays, p.MaxBalance, p.Expiry, p.ExpiryMonth, p.ExpiryAfterMonths,
        p.OnSeparation, p.RehireRule, p.RehireBridgeDays);

    /// <summary>
    /// The client's pre-policy settings as a policy: the flat monthly rate on the 1st, to
    /// Regular/Probationary/Contract staff, after one month of service (Tenure basis) or with 20
    /// present days in the month (Present-Days basis). Null when the client has no rate.
    /// </summary>
    public static UniformPolicySnapshot? FromLegacy(Client client)
    {
        if (client.UniformAllowance is not > 0) return null;
        var presentDays = client.UniformAllowanceBasis == BenefitAccrualBasis.PresentDays;
        return new UniformPolicySnapshot(
            PolicyId: null,
            EffectiveFrom: DateOnly.MinValue,
            IsEnabled: true,
            Frequency: UniformCreditFrequency.Monthly,
            CreditDay: 1,
            StartMonth: 1,
            AmountMode: UniformAmountMode.FixedPerPeriod,
            Amount: client.UniformAllowance.Value,
            ExpectedDaysPerPeriod: null,
            EligibleStatuses: LegacyStatuses,
            WaitingMonths: presentDays ? 0 : 1,
            MinPresentDays: presentDays ? LegacyPresentDaysThreshold : null,
            MaxBalance: null,
            Expiry: UniformBalanceExpiry.CarryOver,
            ExpiryMonth: null,
            ExpiryAfterMonths: null,
            OnSeparation: UniformSeparationRule.KeepForRelease,
            RehireRule: UniformRehireRule.Restart,
            RehireBridgeDays: null);
    }
}

public static class UniformAllowancePolicyResolver
{
    /// <summary>
    /// The client's rules on <paramref name="asOf"/>: the latest version starting on or before it,
    /// else (before the first version starts) the legacy settings, else null -- no allowance.
    /// </summary>
    public static UniformPolicySnapshot? Resolve(
        IEnumerable<UniformAllowancePolicy> versions, Client client, DateOnly asOf)
    {
        var version = versions
            .Where(v => v.ClientId == client.Id && v.EffectiveFrom <= asOf)
            .MaxBy(v => v.EffectiveFrom);
        return version != null ? UniformPolicySnapshot.From(version) : UniformPolicySnapshot.FromLegacy(client);
    }
}
