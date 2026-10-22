using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;

namespace Hrms.Core.Services.UniformAllowance;

// ── Amount (Strategy, one per UniformAmountMode) ─────────────────────────────────────────────

public interface IUniformAmountRule
{
    decimal Earned(UniformPolicySnapshot policy, int presentDays);
}

public sealed class FixedPerPeriodAmount : IUniformAmountRule
{
    public decimal Earned(UniformPolicySnapshot policy, int presentDays) => policy.Amount;
}

public sealed class ProratedByPresentDaysAmount : IUniformAmountRule
{
    public decimal Earned(UniformPolicySnapshot policy, int presentDays)
    {
        if (policy.ExpectedDaysPerPeriod is not > 0) return policy.Amount;
        var share = Math.Min((decimal)presentDays / policy.ExpectedDaysPerPeriod.Value, 1m);
        return Math.Round(policy.Amount * share, 2, MidpointRounding.AwayFromZero);
    }
}

public sealed class PerPresentDayAmount : IUniformAmountRule
{
    public decimal Earned(UniformPolicySnapshot policy, int presentDays) => policy.Amount * presentDays;
}

public static class UniformAmountRules
{
    private static readonly Dictionary<UniformAmountMode, IUniformAmountRule> Rules = new()
    {
        [UniformAmountMode.FixedPerPeriod] = new FixedPerPeriodAmount(),
        [UniformAmountMode.ProratedByPresentDays] = new ProratedByPresentDaysAmount(),
        [UniformAmountMode.PerPresentDay] = new PerPresentDayAmount(),
    };

    public static IUniformAmountRule For(UniformAmountMode mode) => Rules[mode];
}

// ── Eligibility (Chain of Responsibility: the first check that fails names the reason) ───────

/// <summary>One employee on one credit date, as the eligibility checks see them.</summary>
public sealed record UniformCreditCandidate(
    UniformPolicySnapshot Policy,
    EmploymentStatus Status,
    bool IsSeparated,
    DateOnly ServiceStart,
    DateOnly CreditDate,
    int PresentDays);

public enum UniformSkipReason
{
    Separated,
    OnLeave,
    Suspended,
    StatusNotEligible,
    WaitingPeriod,
    NotEnoughPresentDays,
    NoPresentDays,
    BalanceAtCap,
    AlreadyCreditedThisMonth,
}

public sealed record UniformSkip(UniformSkipReason Reason, string Text);

public interface IUniformEligibilityCheck
{
    UniformSkip? Check(UniformCreditCandidate candidate);
}

public sealed class NotSeparatedCheck : IUniformEligibilityCheck
{
    public UniformSkip? Check(UniformCreditCandidate c) =>
        c.IsSeparated ? new(UniformSkipReason.Separated, "Separated from the company") : null;
}

/// <summary>Leave and suspension pause credits with their own reason, whatever the eligible list says.</summary>
public sealed class EligibleStatusCheck : IUniformEligibilityCheck
{
    public UniformSkip? Check(UniformCreditCandidate c) => c.Status switch
    {
        _ when c.Policy.EligibleStatuses.Contains(c.Status) => null,
        EmploymentStatus.OnLeave => new(UniformSkipReason.OnLeave, "On leave — credits pause until they return"),
        EmploymentStatus.Suspended => new(UniformSkipReason.Suspended, "Suspended — credits pause until reinstated"),
        _ => new(UniformSkipReason.StatusNotEligible, $"{c.Status} employees don't qualify"),
    };
}

public sealed class WaitingPeriodCheck : IUniformEligibilityCheck
{
    public UniformSkip? Check(UniformCreditCandidate c)
    {
        if (c.Policy.WaitingMonths <= 0) return null;
        var served = LeaveEligibilityCalculator.MonthsBetween(c.ServiceStart, c.CreditDate);
        return served >= c.Policy.WaitingMonths
            ? null
            : new(UniformSkipReason.WaitingPeriod,
                $"Waiting period: {served} of {c.Policy.WaitingMonths} month(s) of service");
    }
}

public sealed class PresentDaysThresholdCheck : IUniformEligibilityCheck
{
    public UniformSkip? Check(UniformCreditCandidate c) =>
        c.Policy.MinPresentDays is { } required && c.PresentDays < required
            ? new(UniformSkipReason.NotEnoughPresentDays, $"{c.PresentDays} of {required} required present days")
            : null;
}

/// <summary>A credit decision: what to credit (0 when skipped) and why it isn't the full amount.</summary>
public sealed record UniformCreditDecision(UniformCreditOutcome Outcome, decimal Amount, UniformSkip? Reason);

public static class UniformCreditDecider
{
    private static readonly IUniformEligibilityCheck[] Chain =
    [
        new NotSeparatedCheck(),
        new EligibleStatusCheck(),
        new WaitingPeriodCheck(),
        new PresentDaysThresholdCheck(),
    ];

    /// <summary>
    /// Runs the eligibility chain, then the amount rule, then the balance cap: skipped with the
    /// first failing reason, credited in full, or capped to what the cap leaves room for.
    /// </summary>
    public static UniformCreditDecision Decide(UniformCreditCandidate candidate, decimal currentBalance)
    {
        if (FirstIneligibility(candidate) is { } skip) return Skipped(skip);

        var policy = candidate.Policy;
        var earned = UniformAmountRules.For(policy.AmountMode).Earned(policy, candidate.PresentDays);
        if (earned <= 0) return Skipped(new(UniformSkipReason.NoPresentDays, "No present days in the period"));

        if (policy.MaxBalance is not { } cap) return new(UniformCreditOutcome.Credited, earned, null);

        var room = Math.Max(cap - currentBalance, 0);
        if (room <= 0)
            return Skipped(new(UniformSkipReason.BalanceAtCap, $"Balance already at the ₱{cap:N2} cap"));
        if (earned <= room) return new(UniformCreditOutcome.Credited, earned, null);
        return new(UniformCreditOutcome.Capped, room, new(UniformSkipReason.BalanceAtCap,
            $"Capped at the ₱{cap:N2} balance limit — ₱{earned - room:N2} of ₱{earned:N2} not credited"));
    }

    /// <summary>The first eligibility check the candidate fails, in chain order; null when they qualify.</summary>
    public static UniformSkip? FirstIneligibility(UniformCreditCandidate candidate)
    {
        foreach (var check in Chain)
        {
            if (check.Check(candidate) is { } skip) return skip;
        }
        return null;
    }

    private static UniformCreditDecision Skipped(UniformSkip reason) => new(UniformCreditOutcome.Skipped, 0, reason);
}

// ── Separation, rehire and expiry ───────────────────────────────────────────────────────────

public static class UniformSeparation
{
    private static readonly HashSet<EmploymentStatus> SeparatedStatuses =
    [
        EmploymentStatus.Terminated, EmploymentStatus.Resigned, EmploymentStatus.Retired, EmploymentStatus.Deceased,
    ];

    /// <summary>A separated status, or a resignation date on or before the day.</summary>
    public static bool IsSeparated(EmploymentStatus status, DateTime? dateResigned, DateOnly asOf) =>
        SeparatedStatuses.Contains(status) ||
        (dateResigned is { } resigned && DateOnly.FromDateTime(resigned) <= asOf);

    /// <summary>The day separation took effect: the resignation date, else the day it was noticed.</summary>
    public static DateOnly SeparatedOn(DateTime? dateResigned, DateOnly noticedOn) =>
        dateResigned is { } resigned && DateOnly.FromDateTime(resigned) <= noticedOn
            ? DateOnly.FromDateTime(resigned)
            : noticedOn;

    /// <summary>
    /// Where service counts from once a separated employee is active again. The rehire date is the
    /// new hire date when HR moved it past the separation, else the day the return was noticed.
    /// Bridged rehires inside the window keep the earlier start.
    /// </summary>
    public static DateOnly ServiceStartAfterRehire(
        UniformPolicySnapshot policy, DateOnly previousStart, DateOnly hireDate, DateOnly separatedOn, DateOnly noticedOn)
    {
        var rehiredOn = hireDate > separatedOn ? hireDate : noticedOn;
        var bridged = policy.RehireRule == UniformRehireRule.BridgeWithinDays &&
                      policy.RehireBridgeDays is { } days &&
                      rehiredOn.DayNumber - separatedOn.DayNumber <= days;
        return bridged ? previousStart : rehiredOn;
    }
}

public static class UniformExpiry
{
    /// <summary>Whether the policy may expire anything on this day (reset day, or any 1st for rolling expiry).</summary>
    public static bool RunsOn(UniformPolicySnapshot policy, DateOnly date) => policy.Expiry switch
    {
        UniformBalanceExpiry.ResetAnnually => date.Day == 1 && date.Month == policy.ExpiryMonth,
        UniformBalanceExpiry.ExpireAfterMonths => date.Day == 1 && policy.ExpiryAfterMonths is > 0,
        _ => false,
    };

    /// <summary>The first day whose credits are still fresh under rolling expiry.</summary>
    public static DateOnly FreshSince(UniformPolicySnapshot policy, DateOnly date) =>
        date.AddMonths(-(policy.ExpiryAfterMonths ?? 0));

    /// <summary>
    /// What lapses today. A yearly reset clears the whole balance. Rolling expiry lapses whatever
    /// the balance holds beyond the credits added since <see cref="FreshSince"/> -- spending is
    /// taken from the oldest money first, so only old money can be left over.
    /// </summary>
    public static decimal Due(UniformPolicySnapshot policy, DateOnly date, decimal balance, decimal freshCredits)
    {
        if (balance <= 0 || !RunsOn(policy, date)) return 0;
        return policy.Expiry == UniformBalanceExpiry.ResetAnnually
            ? balance
            : Math.Max(balance - Math.Max(freshCredits, 0), 0);
    }
}
