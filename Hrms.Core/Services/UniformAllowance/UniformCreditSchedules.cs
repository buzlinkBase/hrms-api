using Hrms.Domain.Entities;

namespace Hrms.Core.Services.UniformAllowance;

/// <summary>
/// When a policy's credits post and which attendance window each one judges (Strategy, one per
/// <see cref="UniformCreditFrequency"/>). A credit always judges the period that just ended, so
/// its present days are final.
/// </summary>
public interface IUniformCreditSchedule
{
    bool IsDue(DateOnly date, UniformPolicySnapshot policy, DateOnly serviceStart);
    (DateOnly From, DateOnly To) PeriodFor(DateOnly creditDate, UniformPolicySnapshot policy);
}

/// <summary>
/// Monthly, quarterly, semi-annual and annual credits: on CreditDay of StartMonth and every
/// <c>months</c> months after it, covering the <c>months</c> calendar months before the credit.
/// </summary>
public sealed class CalendarCycleSchedule(int months) : IUniformCreditSchedule
{
    public int Months { get; } = months;

    public bool IsDue(DateOnly date, UniformPolicySnapshot policy, DateOnly serviceStart) =>
        date.Day == policy.CreditDay && ((date.Month - policy.StartMonth) % Months + Months) % Months == 0;

    public (DateOnly From, DateOnly To) PeriodFor(DateOnly creditDate, UniformPolicySnapshot policy)
    {
        var monthStart = new DateOnly(creditDate.Year, creditDate.Month, 1);
        return (monthStart.AddMonths(-Months), monthStart.AddDays(-1));
    }
}

/// <summary>
/// Once a year on the day service started (Feb 29 starts are credited Feb 28 in other years),
/// covering the year that just ended.
/// </summary>
public sealed class HireAnniversarySchedule : IUniformCreditSchedule
{
    public bool IsDue(DateOnly date, UniformPolicySnapshot policy, DateOnly serviceStart)
    {
        if (date <= serviceStart || date.Month != serviceStart.Month) return false;
        var day = Math.Min(serviceStart.Day, DateTime.DaysInMonth(date.Year, date.Month));
        return date.Day == day;
    }

    public (DateOnly From, DateOnly To) PeriodFor(DateOnly creditDate, UniformPolicySnapshot policy) =>
        (creditDate.AddYears(-1), creditDate.AddDays(-1));
}

public static class UniformCreditSchedules
{
    private static readonly Dictionary<UniformCreditFrequency, IUniformCreditSchedule> Schedules = new()
    {
        [UniformCreditFrequency.Monthly] = new CalendarCycleSchedule(1),
        [UniformCreditFrequency.Quarterly] = new CalendarCycleSchedule(3),
        [UniformCreditFrequency.SemiAnnual] = new CalendarCycleSchedule(6),
        [UniformCreditFrequency.Annual] = new CalendarCycleSchedule(12),
        [UniformCreditFrequency.HireAnniversary] = new HireAnniversarySchedule(),
    };

    public static IUniformCreditSchedule For(UniformCreditFrequency frequency) => Schedules[frequency];

    /// <summary>
    /// The next credit dates on or after <paramref name="from"/>, each with the rules in force on
    /// that day -- so a future version changes the dates after it starts. Looks at most three
    /// years ahead (an annual cycle shows at least two).
    /// </summary>
    public static IEnumerable<(DateOnly Date, UniformPolicySnapshot Policy)> Upcoming(
        Func<DateOnly, UniformPolicySnapshot?> policyOn, DateOnly from, DateOnly serviceStart, int count)
    {
        var found = 0;
        for (var date = from; date <= from.AddYears(3) && found < count; date = date.AddDays(1))
        {
            var policy = policyOn(date);
            if (policy is not { IsEnabled: true } || !For(policy.Frequency).IsDue(date, policy, serviceStart)) continue;
            found++;
            yield return (date, policy);
        }
    }
}
