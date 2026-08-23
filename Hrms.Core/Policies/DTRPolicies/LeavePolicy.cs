using Hrms.Domain.Entities.EmployeeEntities;

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
        if (dailyRecord.ShiftWorkingHour <= 0 || dailyRecord.LateMinutes <= 0)
        {
            return linecollection;
        }
        var employee = context.Employee;
        var hourlyRate = employee.DailyRate / (decimal)dailyRecord.ShiftWorkingHour;

        //line.Value += hourlyRate * regularHours;

        linecollection.Add(new BasicPipelineData
        {
            PayType = PayType.WithoutPay,
            Value = hourlyRate * (decimal)dailyRecord.UnpaidLeaveHours,
            LeaveInfo = new LeaveInfo
            {
                ConsumeCredit = 0,
                //leaveFraction = (decimal)dailyRecord.UnpaidLeaveHours,
            }
        });
        linecollection.Add(new BasicPipelineData
        {
            PayType = PayType.WithPay,
            Value = hourlyRate * (decimal)dailyRecord.PaidLeaveHours,
            LeaveInfo = new LeaveInfo
            {
                ConsumeCredit = dailyRecord.PaidLeaveHours,
                //leaveFraction = (decimal)dailyRecord.PaidLeaveHours,
            }
        });
        return linecollection;
        //var key = new Leavekey(context.Employee.Id);
        //if (!context.Payload.Leaves.TryGetValue(key, out var leaves) || leaves == null || !leaves.Any())
        //    return linecollection;

        //var dailyRate = context.Employee.DailyRate;

        //foreach (var leave in leaves)
        //{
        //    var line = new BasicPipelineData
        //    {
        //        PayType = leave.PayType
        //    };
        //    // Compute leave duration (whole day = 1.0, half day = 0.5)
        //    decimal leaveFraction = leave.DayFraction == DayFraction.FullDay ? 1.0m : 0.5m;
        //    line.LeaveInfo.leaveFraction = leaveFraction;

        //    var creditKey = new EmployeeLeaveCreditsKey(context.Employee.Id, leave.LeaveId);
        //    line.LeaveInfo.LeaveId = leave.LeaveId;

        //    //TODO maybe we can transfer this validation on leave approval
        //    if (!context.Payload.LeaveCredits.TryGetValue(creditKey, out var totalCredits) || totalCredits <= 0)
        //    {
        //        // No credits → convert to unpaid leave
        //        line.PayType = PayType.WithoutPay;
        //        line.Value = 0;
        //        line.LeaveInfo.ConsumeCredit = 0;
        //    }
        //    else
        //    {
        //        line.Value = dailyRate * leaveFraction;
        //        line.LeaveInfo.ConsumeCredit = (double)leaveFraction;
        //    }

        //    linecollection.Add(line);
        //}
        //return linecollection;
    }
}