using Hrms.Core.Policies.DeductionPolicies;

namespace Hrms.Core.Calculators;

public class BasicPayrollCalculator : ICalculator<BasicRateModel, PayrollContext>
{
    public BasicRateModel Calculate(PayrollContext context)
    {
        var regularResult = new RegularPipeline().Run(context).Value;
        var restResult = new RestDayPipeLine().Run(context).Value;
        var legalHolResult = new RegularHolidayPipeLine().Run(context).Value;
        var specialHolResult = new SpecialHolidayPipeline().Run(context).Value;
        var otResult = new OvertimePipeline().Run(context);
        var nightDiffResult = new NightDiffPipeline().Run(context);
        var leaveResult = new LeavePipeline().Run(context);
        var absentResult = new AbsentPipeline().Run(context);
        var lateResult = new LatesPipeLine().Run(context);
        var utResult = new UnderTimePipeLine().Run(context);
        var lwop = leaveResult.Where(x => x.PayType == PayType.WithoutPay).Sum(x => x.Value);
        var leaveWithPay = leaveResult.Where(x => x.PayType == PayType.WithPay).Sum(x => x.Value);

        var payrollModel = new BasicRateModel
        {
            Basic = regularResult + leaveWithPay,
            RegularDuty = regularResult,
            LeaveWithPay = leaveWithPay,
            RestDayDuty = restResult,
            AbsentInfo = absentResult.AbsentInfo,
            LegalHoliday = legalHolResult,
            SpecialHoliday = specialHolResult,
            LateHourInfo = lateResult.LateInfo,
            UTHourInfo = utResult.UTInfo,
            OTHourInfo = otResult.OTInfo,
            LWOP = lwop,
            NightDiffInfo = nightDiffResult.NightDiffInfo,
            Leaves = leaveResult.Select(x => x.LeaveInfo).ToList(),
            Date = context.PayrollDate,
            EmployeeId = context.Employee.Id,
            TimeBaseGross = regularResult
                            + leaveWithPay
                            + restResult
                            + otResult.Value
                            + specialHolResult
                            + nightDiffResult.Value
                            + legalHolResult
        };
        return payrollModel;
    }
}
