namespace Hrms.Core.Extensions
{
    public static class DateExtensions
    {
        public static decimal GetDailyCompute(this decimal amount, DateOnly date)
        {
            var daysInMonth = date.GetDaysInMonth();
            return amount / daysInMonth;
        }
        public static int GetDaysInMonth(this DateOnly date)
        {
            return DateTime.DaysInMonth(date.Year, date.Month);
        }

        /// <summary>
        /// Gets the number of remaining weeks in the month starting from the given date.
        /// </summary>
        public static int GetRemainingWeeksInMonth(this DateOnly pyFromDate)
        {
            var fromDate = pyFromDate.ToDateTime(TimeOnly.MinValue);
            // Last day of the month
            var lastDayOfMonth = new DateTime(fromDate.Year, fromDate.Month,
                DateTime.DaysInMonth(fromDate.Year, fromDate.Month));

            // Total days remaining including current day
            int daysRemaining = (lastDayOfMonth - fromDate).Days + 1;

            // Convert to weeks (round up to ensure partial weeks count as full)
            int remainingWeeks = (int)Math.Ceiling(daysRemaining / 7.0);

            return remainingWeeks;
        }

        public static int GetNumberOfWeeksInMonth(this DateOnly date, DayOfWeek firstDayOfWeek = DayOfWeek.Sunday)
        {
            var firstDayOfMonth = new DateOnly(date.Year, date.Month, 1);
            var daysInMonth = DateTime.DaysInMonth(date.Year, date.Month);

            // Offset: how many days from the chosen firstDayOfWeek until the month starts
            int offset = ((int)firstDayOfMonth.DayOfWeek - (int)firstDayOfWeek + 7) % 7;

            // Total span = offset + days in month
            int totalSpan = offset + daysInMonth;

            // Divide into weeks
            return (int)Math.Ceiling(totalSpan / 7.0);
        }

        public static int GetWeekOfMonth(this DateOnly date, DayOfWeek firstDayOfWeek = DayOfWeek.Sunday)
        {
            var firstDayOfMonth = new DateOnly(date.Year, date.Month, 1);

            // Offset relative to chosen first day of week
            int offset = ((int)firstDayOfMonth.DayOfWeek - (int)firstDayOfWeek + 7) % 7;

            int dayNumber = date.Day + offset;
            return ((dayNumber - 1) / 7) + 1;
        }


        public static bool IsLastWeekOfMonth(this DateOnly date)
        {
            // Get the last day of the month
            DateOnly lastDayOfMonth = new DateOnly(date.Year, date.Month, date.GetDaysInMonth());
            // Find the start of the week for the last day of the month (assuming Monday as start)
            int diff = (7 + (lastDayOfMonth.DayOfWeek - DayOfWeek.Monday)) % 7;
            DateOnly startOfLastWeek = lastDayOfMonth.AddDays(-diff);
            // If the given date is on or after the start of the last week, it's in the last week
            return date >= startOfLastWeek;
        }



        public static double GetTotalDaysDiff(this DateOnly fromDate, DateOnly toDate)
        {
            var d1 = fromDate.ToDateTime(TimeOnly.MinValue);
            var d2 = toDate.ToDateTime(TimeOnly.MinValue);
            return (d2 - d1).TotalDays;

        }
    }
}
