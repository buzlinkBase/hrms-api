namespace Hrms.Core.Services;

// Pure "nth weekday of month" date math for Holiday.WeekOfMonth/DayOfWeek — e.g. National
// Heroes Day = "last Monday of August". internal static so it's directly unit-testable
// without a database, same convention as ThirteenthMonthCeilingCalculator.
public static class HolidayRecurrenceCalculator
{
    // weekOfMonth: 1-4 = first..fourth occurrence of dayOfWeek in the month, 5 = last
    // occurrence in the month.
    internal static DateOnly ResolveNthWeekday(int year, int month, DayOfWeek dayOfWeek, int weekOfMonth)
    {
        if (weekOfMonth == 5)
        {
            var lastDayOfMonth = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
            var offsetFromEnd = ((int)lastDayOfMonth.DayOfWeek - (int)dayOfWeek + 7) % 7;
            return lastDayOfMonth.AddDays(-offsetFromEnd);
        }

        var firstDayOfMonth = new DateOnly(year, month, 1);
        var offsetFromStart = ((int)dayOfWeek - (int)firstDayOfMonth.DayOfWeek + 7) % 7;
        var firstOccurrence = firstDayOfMonth.AddDays(offsetFromStart);
        return firstOccurrence.AddDays((weekOfMonth - 1) * 7);
    }
}
