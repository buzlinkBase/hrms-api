namespace Hrms.Core.Policies.DTRPolicies;

internal class LeavePolicy : PayrollPolicyBase<LineCollection<BasicPipelineData>, PayrollContext>
{
    //TODO IF THE LEAVE DAY IF FALL ON HOLIDAY, DONT DEDUCT FROM CREDITS AND EMPLOYEE MAY STILL GET THE
    //HOLIDAY PAY IF THEY ARE ELIGIBLE
    //.AndNot(new IsRegularHoliday())
    public LeavePolicy() : base(new IsLeave()
        .AndNot(new IsLegalHolidayDay())
        .AndNot(new IsSpecialNonWorkingDay())
        .AndNot(new IsRestDayType())
        .And(new IsEligibleForLeaveCredits()))
    { }


    public override LineCollection<BasicPipelineData> ApplyIfSatisfied(LineCollection<BasicPipelineData> linecollection, PayrollContext context)
    {
        var key = new Leavekey(context.Employee.Id);
        // No leave records for this employee/pay date
        if (!context.Payload.Leaves.TryGetValue(key, out var leaves) || leaves == null || !leaves.Any())
            return linecollection;

        var dailyRate = context.Employee.DailyRate;

        foreach (var leave in leaves)
        {
            var line = new BasicPipelineData
            {
                PayType = leave.PayType
            };
            // Compute leave duration (whole day = 1.0, half day = 0.5)
            decimal leaveFraction = leave.DayFraction == DayFraction.FullDay ? 1.0m : 0.5m;
            line.LeaveInfo.leaveFraction = leaveFraction;

            // Check available leave credits
            var creditKey = new EmployeeLeaveCreditsKey(context.Employee.Id, leave.LeaveId);
            line.LeaveInfo.LeaveId = leave.LeaveId;

            //TODO maybe we can transfer this validation on leave approval
            if (!context.Payload.LeaveCredits.TryGetValue(creditKey, out var totalCredits) || totalCredits <= 0)
            {
                // No credits → convert to unpaid leave
                line.PayType = PayType.WithoutPay;
                line.Value = 0;
                line.LeaveInfo.ConsumeCredit = 0;
            }
            else
            {
                line.Value = dailyRate * leaveFraction;
                line.LeaveInfo.ConsumeCredit = (double)leaveFraction;
            }

            linecollection.Add(line);
        }
        return linecollection;
    }
}