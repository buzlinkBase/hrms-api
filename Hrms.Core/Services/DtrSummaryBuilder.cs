namespace Hrms.Core.Services;

/// <summary>
/// Boils calculated DTR day rows down to one summary per employee -- the same totals the Calculate
/// DTR review table has always shown, now computed where the rows already are.
/// </summary>
public static class DtrSummaryBuilder
{
    public static List<DtrEmployeeSummaryModel> Build(IEnumerable<DTRDetailModel> rows) =>
        rows
            .GroupBy(r => r.EmployeeId)
            .Select(g => new DtrEmployeeSummaryModel
            {
                EmployeeId = g.Key,
                FullName = g.Select(x => x.FullName).FirstOrDefault(n => !string.IsNullOrEmpty(n)),
                WorkDates = g.Select(x => x.WorkDate).Distinct().Order().ToList(),
                RegularHours = g.Sum(x => x.RegularNetHours),
                OtHours = g.Sum(x => x.RegularOTHours + x.RestDayOTHours),
                NdHours = g.Sum(x => x.RegularNDHours + x.RestDayNDHours),
                LateMinutes = g.Sum(x => x.LateMinutes),
                UtMinutes = g.Sum(x => x.UTMinutes),
                Absences = g.Sum(x => x.AbsentCount),
                LeaveHours = g.Sum(x => x.PaidLeaveHours + x.UnpaidLeaveHours),
                MissingPunchDays = g.Where(x => (x.StartTime != null) != (x.EndTime != null))
                    .Select(x => x.WorkDate).Distinct().Count(),
            })
            .OrderBy(s => s.FullName ?? s.EmployeeId.ToString(), StringComparer.OrdinalIgnoreCase)
            .ToList();
}
