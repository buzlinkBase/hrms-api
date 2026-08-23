namespace Hrms.Core.Calculators;

/// <summary>
/// Resolves the effective Daily Rate to use for a payroll run.
/// Manual mode (or non-FIXED salary): the employee's stored DailyRate is authoritative.
/// CalculatedEDR mode: DailyRate = (MonthlyRate * 12) / FactorDays — FactorDays is an
/// annual factor (e.g. 365, 313, 261, 252, or an international/continuous-ops variant).
/// MonthlyTotalDays mode: DailyRate = MonthlyRate / divisor, where the divisor is either
///  - the actual number of days in the payroll month (28/29/30/31), when UseActualMonthDays
///    is set — resolved per run from <paramref name="referenceDate"/>, or
///  - a flat FactorDays denominator (e.g. 30.4167, 30, 31, 26, 22, 21.67) otherwise.
/// Called once per employee, per payroll run, before the day-by-day DTR policy loop —
/// the resolved value is written back onto the employee model so every policy that reads
/// employee.DailyRate downstream (RestDayPolicy, RegularWorkDayPolicy, StatutoryHelper, etc.)
/// automatically gets the resolved rate without each needing to call the resolver itself.
/// </summary>
public static class DailyRateResolver
{
    public static decimal Resolve(EmployeeModelPayrollRun employee, DateOnly referenceDate = default)
    {
        if (employee.SalaryType != SalaryType.FIXED) return employee.DailyRate;

        switch (employee.DailyRateMode)
        {
            case DailyRateMode.CalculatedEDR:
                if (employee.FactorDays is not > 0) return employee.DailyRate;
                return Round((employee.MonthlyRate * 12m) / employee.FactorDays.Value);

            case DailyRateMode.MonthlyTotalDays:
                if (employee.UseActualMonthDays)
                {
                    var refDate = referenceDate == default ? DateOnly.FromDateTime(DateTime.UtcNow) : referenceDate;
                    var daysInMonth = DateTime.DaysInMonth(refDate.Year, refDate.Month);
                    return Round(employee.MonthlyRate / daysInMonth);
                }
                if (employee.FactorDays is not > 0) return employee.DailyRate;
                return Round(employee.MonthlyRate / employee.FactorDays.Value);

            default:
                return employee.DailyRate;
        }
    }

    private static decimal Round(decimal v) => Math.Round(v, 4, MidpointRounding.AwayFromZero);
}
