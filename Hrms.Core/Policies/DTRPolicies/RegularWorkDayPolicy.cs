using Hrms.Core.Calculators.DtrPay;

namespace Hrms.Core.Policies.DTRPolicies;

// Regular work day: hours x plain hourly rate (day rate 1.0).
//   Additive:   natural RegularNetHours (night included) -- the ND bucket adds only the premium.
//   Compounded: plain hours only -- the ND bucket pays the night hours at 1.0 x ND.
internal class RegularWorkDayPolicy : PayrollPolicyBase<BasicPipelineData, PayrollContext>
{
    public RegularWorkDayPolicy() { }

    public override BasicPipelineData ApplyIfSatisfied(BasicPipelineData line, PayrollContext context)
    {
        //include hourly computation for FIXED employee here for ND Premium extraction later
        var dailyRecord = context.DailyRecord;
        if (dailyRecord.ShiftWorkingHour <= 0)
        {
            return line;
        }

        var hours = DayTypeHours.For(context, dailyRecord.RegularNetHours, dailyRecord.RegularNDHours, dailyRecord.RegularNDOTHours);
        if (hours.Plain > 0)
        {
            line.Value += RateHelper.GetHourlyRate(context) * hours.Plain;
        }

        // A Fixed-salary employee's Basic Pay is the monthly salary, not this day pay
        // (EmployeePayrollLineService.GetBasicPay), while their regular night hours' base has
        // always been paid on top of it through the ND bucket -- so for them the night hours
        // stay there (AdditiveRegularDayPay) instead of being paid here.
        return context.Employee.SalaryType == SalaryType.FIXED
            ? line
            : hours.AddNightPay(line, context, PayCategories.Regular);
    }
}
