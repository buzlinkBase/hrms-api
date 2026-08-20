namespace DTR.Core.Extensions;

//Common DTR Rounding Strategies
//15 - Minute Block Rounding(Nearest / Standard 7/8 Rule): Rounds to the nearest 15 - minute mark(:00, :15, :30, :45).
//15-Minute Floor(Strict / Employee Favorite): Always rounds down to the previous 15 - minute mark(prevents paying unapproved partial blocks).
//15-Minute Ceiling: Always rounds up to the next 15 - minute mark.
//Grace Period Threshold: Ignores small variances(e.g., punching in 5 minutes early or late does not adjust baseline scheduled hours).

public static class RoundingHoursExtensions
{
    public static TimeSpan RoundToNearestMinutes(this TimeSpan time, int minuteInterval)
    {
        double totalMinutes = time.TotalMinutes;
        double roundedMinutes = Math.Round(totalMinutes / minuteInterval) * minuteInterval;
        return TimeSpan.FromMinutes(roundedMinutes);
    }

    // 2. Round Down (Floor) to N Minutes
    public static TimeSpan RoundDownMinutes(this TimeSpan time, int minuteInterval)
    {
        double totalMinutes = time.TotalMinutes;
        double roundedMinutes = Math.Floor(totalMinutes / minuteInterval) * minuteInterval;
        return TimeSpan.FromMinutes(roundedMinutes);
    }

    // 3. Convert TimeSpan to Decimal Hours (e.g., 8h 30m -> 8.5)
    public static decimal ToDecimalHours(this TimeSpan time)
    {
        return (decimal)Math.Round(time.TotalHours, 2);
    }
}
public class DtrCalculator
{
    public decimal CalculateDailyHours(DateTime timeIn, DateTime timeOut, TimeSpan breakDuration)
    {
        // 1. Calculate raw worked duration
        TimeSpan rawWorkedTime = (timeOut - timeIn) - breakDuration;

        if (rawWorkedTime < TimeSpan.Zero)
            return 0m;

        // 2. Apply 15-minute nearest rounding policy
        TimeSpan roundedTime = rawWorkedTime.RoundToNearestMinutes(15);

        // 3. Return final hours as a decimal for payroll processing
        return roundedTime.ToDecimalHours();
    }
}
// Example Usage:
//var calculator = new DtrCalculator();
//    // Employee works 8 hours, 7 minutes (08:00 AM to 04:37 PM, 30m break)
//    DateTime timeIn = DateTime.Parse("2026-08-21 08:00:00");
//    DateTime timeOut = DateTime.Parse("2026-08-21 16:37:00");
//    TimeSpan breakTime = TimeSpan.FromMinutes(30);
//    decimal payableHours = calculator.CalculateDailyHours(timeIn, timeOut, breakTime);
//// Output: 8.00 hours (8h 07m rounds down to 8h 00m)