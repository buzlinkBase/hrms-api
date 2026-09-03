using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hrms.Core.Utilities;

using System;
using System.Collections.Generic;

public class DateRange
{
    public DateTime Start { get; set; }
    public DateTime End { get; set; }

    public DateRange(DateTime start, DateTime end)
    {
        if (end < start)
            throw new ArgumentException("End date cannot be earlier than start date.");

        // Stripping time components to ensure pure date-level comparisons
        Start = start.Date;
        End = end.Date;
    }
}

public static class DateIntersectionUtility
{
    /// <summary>
    /// Checks if two date ranges overlap.
    /// </summary>
    public static bool HasIntersection(DateRange range1, DateRange range2)
    {
        return range1.Start <= range2.End && range1.End >= range2.Start;
    }

    /// <summary>
    /// Gets the intersecting DateRange between Leave and Payroll. Returns null if no intersection.
    /// </summary>
    public static DateRange GetIntersectionRange(DateRange leave, DateRange payroll)
    {
        if (!HasIntersection(leave, payroll))
            return null;

        DateTime intersectionStart = leave.Start > payroll.Start ? leave.Start : payroll.Start;
        DateTime intersectionEnd = leave.End < payroll.End ? leave.End : payroll.End;

        return new DateRange(intersectionStart, intersectionEnd);
    }

    /// <summary>
    /// Returns every individual date from the leave that falls within the payroll period.
    /// </summary>
    public static IEnumerable<DateTime> GetIntersectedDates(DateRange leave, DateRange payroll)
    {
        var intersection = GetIntersectionRange(leave, payroll);
        if (intersection == null)
            yield break;

        for (var date = intersection.Start; date <= intersection.End; date = date.AddDays(1))
        {
            yield return date;
        }
    }

}