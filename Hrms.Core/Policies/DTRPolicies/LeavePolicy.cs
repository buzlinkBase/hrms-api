namespace Hrms.Core.Policies.DTRPolicies;

internal class LeavePolicy : PayrollPolicyBase<LineCollection<BasicPipelineData>, PayrollContext>
{
    //public LeavePolicy() : base(new IsLeave()
    //    .AndNot(new IsLegalHolidayDay())
    //    .AndNot(new IsSpecialNonWorkingDay())
    //    .AndNot(new IsRestDayType())
    //    .And(new IsEligibleForLeaveCredits()))
    //{ }

    public override LineCollection<BasicPipelineData> ApplyIfSatisfied(LineCollection<BasicPipelineData> linecollection, PayrollContext context)
    {
        var dailyRecord = context.DailyRecord;
        // Ensure valid shift hours and that there are actual leave hours recorded
        if (dailyRecord.ShiftWorkingHour <= 0 ||
           (dailyRecord.UnpaidLeaveHours <= 0 && dailyRecord.PaidLeaveHours <= 0))
        {
            return linecollection;
        }

        var employee = context.Employee;
        var hourlyRate = RateHelper.GetHourlyRate(context);

        if (dailyRecord.UnpaidLeaveHours > 0)
        {
            linecollection.Add(new BasicPipelineData
            {
                PayType = PayType.WithoutPay,
                Value = hourlyRate * (decimal)dailyRecord.UnpaidLeaveHours,
            });
        }
        if (dailyRecord.PaidLeaveHours > 0)
        {
            linecollection.Add(new BasicPipelineData
            {
                PayType = PayType.WithPay,
                Value = hourlyRate * (decimal)dailyRecord.PaidLeaveHours,
            });
        }
        return linecollection;
    }
}