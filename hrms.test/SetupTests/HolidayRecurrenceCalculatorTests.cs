using Hrms.Core.Services;

namespace hrms.test.SetupTests;

public class HolidayRecurrenceCalculatorTests
{
    [Fact]
    public void LastMondayOfAugust_2026_ResolvesToAugust31()
    {
        // Aug 31, 2026 is itself a Monday — the last day of the month IS the target weekday.
        var result = HolidayRecurrenceCalculator.ResolveNthWeekday(2026, 8, DayOfWeek.Monday, weekOfMonth: 5);

        result.Should().Be(new DateOnly(2026, 8, 31));
    }

    [Fact]
    public void LastMondayOfAugust_2027_ResolvesToAugust30()
    {
        // Aug 31, 2027 is a Tuesday, so the last Monday falls a day earlier than month-end.
        var result = HolidayRecurrenceCalculator.ResolveNthWeekday(2027, 8, DayOfWeek.Monday, weekOfMonth: 5);

        result.Should().Be(new DateOnly(2027, 8, 30));
    }

    [Fact]
    public void SecondMondayOfSeptember_2025_ResolvesToSeptember8()
    {
        // Sep 1, 2025 is itself a Monday, so the second occurrence is exactly one week later.
        var result = HolidayRecurrenceCalculator.ResolveNthWeekday(2025, 9, DayOfWeek.Monday, weekOfMonth: 2);

        result.Should().Be(new DateOnly(2025, 9, 8));
    }

    [Fact]
    public void ThirdFridayOfMarch_2026_ResolvesToMarch20()
    {
        var result = HolidayRecurrenceCalculator.ResolveNthWeekday(2026, 3, DayOfWeek.Friday, weekOfMonth: 3);

        result.Should().Be(new DateOnly(2026, 3, 20));
    }

    [Fact]
    public void FirstOccurrence_WhenTargetWeekdayIsTheFirstOfMonth_ResolvesToThatDate()
    {
        // Mar 1, 2026 is itself a Sunday.
        var result = HolidayRecurrenceCalculator.ResolveNthWeekday(2026, 3, DayOfWeek.Sunday, weekOfMonth: 1);

        result.Should().Be(new DateOnly(2026, 3, 1));
    }
}
