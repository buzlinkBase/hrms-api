namespace Hrms.Core.Calculators;

public class BasicPayrollCalculator : ICalculator<BasicRateModel, PayrollContext>
{
    public BasicRateModel Calculate(PayrollContext context)
    {
        var regularResult = new RegularPipeline().Run(context).Value;
        var restResult = new RestDayPipeLine().Run(context).Value;
        var legalHolResult = new RegularHolidayPipeLine().Run(context).Value;
        var specialWorkDayResult = new SpecialWorkDayPipeLine().Run(context).Value;
        var restLegalDayResult = new RestLegalDayPipeLine().Run(context).Value;
        var restSpecialDayResult = new RestSpecialDayPipeLine().Run(context).Value;
        var doubleLegalResult = new DoubleLegalPipeLine().Run(context).Value;
        var restDoubleLegalResult = new RestDoubleLegalPipeLine().Run(context).Value;

        var regularOT = new RegularOTPipeLine().Run(context).Value;
        var restDayOT = new RestDayOTPipeLine().Run(context).Value;
        var legalHolOT = new LegalHolOTPipeLine().Run(context).Value;
        var specialNonWorkingOT = new SpecialNonWorkingOTPipeLine().Run(context).Value;
        var restLegalDayOT = new RestLegalDayOTPipeLine().Run(context).Value;
        var restSpecialDayOT = new RestSpecialDayOTPipeLine().Run(context).Value;
        var doubleLegalOT = new DoubleLegalOTPipeLine().Run(context).Value;
        var restDoubleLegalOT = new RestDoubleLegalOTPipeLine().Run(context).Value;

        var otTotal = regularOT
            + restDayOT
            + legalHolOT
            + restLegalDayOT
            + specialNonWorkingOT
            + restSpecialDayOT
            + doubleLegalOT
            + restDoubleLegalOT;

        var otHours = (decimal)(context.DailyRecord.RegularOTHours + context.DailyRecord.RestDayOTHours
            + context.DailyRecord.LegalHolOTHours + context.DailyRecord.RestLegalDayOTHours
            + context.DailyRecord.SpecialHolOTHours + context.DailyRecord.RestSpecialDayOTHours
            + context.DailyRecord.DoubleLegalOTHours + context.DailyRecord.RestDoubleLegalOTHours);

        var regularND = new RegularNDPipeLine().Run(context).Value;
        var restDayND = new RestDayNDPipeLine().Run(context).Value;
        var legalHolND = new LegalHolNDPipeLine().Run(context).Value;
        var specialNonWorkingND = new SpecialNonWorkingNDPipeLine().Run(context).Value;
        var restLegalDayND = new RestLegalDayNDPipeLine().Run(context).Value;
        var restSpecialDayND = new RestSpecialDayNDPipeLine().Run(context).Value;
        var doubleLegalND = new DoubleLegalNDPipeLine().Run(context).Value;
        var restDoubleLegalND = new RestDoubleLegalNDPipeLine().Run(context).Value;

        var ndTotal = regularND
            + restDayND
            + legalHolND
            + restLegalDayND
            + specialNonWorkingND
            + restSpecialDayND
            + doubleLegalND
            + restDoubleLegalND;

        var regularNDOT = new RegularNDOTPipeLine().Run(context).Value;
        var restDayNDOT = new RestDayNDOTPipeLine().Run(context).Value;
        var legalHolNDOT = new LegalHolNDOTPipeLine().Run(context).Value;
        var restLegalDayNDOT = new RestLegalDayNDOTPipeLine().Run(context).Value;
        var specialNonWorkingNDOT = new SpecialNonWorkingNDOTPipeLine().Run(context).Value;
        var restSpecialDayNDOT = new RestSpecialDayNDOTPipeLine().Run(context).Value;
        var doubleLegalNDOT = new DoubleLegalNDOTPipeLine().Run(context).Value;
        var restDoubleLegalNDOT = new RestDoubleLegalNDOTPipeLine().Run(context).Value;

        var ndotTotal = regularNDOT
            + restDayNDOT
            + legalHolNDOT
            + restLegalDayNDOT
            + specialNonWorkingNDOT
            + restSpecialDayNDOT
            + doubleLegalNDOT
            + restDoubleLegalNDOT;

        var ndAndNdotHours = (decimal)(context.DailyRecord.RegularNDHours + context.DailyRecord.RegularNDOTHours
            + context.DailyRecord.RestDayNDHours + context.DailyRecord.RestDayNDOTHours
            + context.DailyRecord.LegalHolNightDiffHours + context.DailyRecord.LegalHolNightDiffOTHours
            + context.DailyRecord.RestLegalDayNDHours + context.DailyRecord.RestLegalDayNDOTHours
            + context.DailyRecord.SpecialHolNightDiffHours + context.DailyRecord.SpecialHolNightDiffOTHours
            + context.DailyRecord.RestSpecialDayNDHours + context.DailyRecord.RestSpecialDayNDOTHours
            + context.DailyRecord.DoubleLegalNDHours + context.DailyRecord.DoubleLegalNDOTHours
            + context.DailyRecord.RestDoubleLegalNDHours + context.DailyRecord.RestDoubleLegalNDOTHours);

        var absentResult = new AbsentPipeline().Run(context);
        var lateResult = new LatesPipeLine().Run(context);
        var utResult = new UnderTimePipeLine().Run(context);
        //var leaveResult = new LeavePipeline().Run(context);
        //var lwop = leaveResult.Where(x => x.PayType == PayType.WithoutPay).Sum(x => x.Value);
        //var leaveWithPay = leaveResult.Where(x => x.PayType == PayType.WithPay).Sum(x => x.Value);
        var leaveWithPay = (decimal)context.DailyRecord.PaidLeaveHours;
        return new BasicRateModel
        {
            DtrId = context.DailyRecord.Id,
            DTRRef = context.DailyRecord.BatchCode,
            Date = context.PayrollDate,
            EmployeeId = context.Employee.Id,
            LWOP = (decimal)context.DailyRecord.UnpaidLeaveHours ,
            LeaveWithPay = leaveWithPay,
            AbsentInfo = absentResult.AbsentInfo,
            BasicPay = regularResult + leaveWithPay,
            RegularDuty = regularResult,
            RegularOT = regularOT,
            RegularND = regularND,
            RegularNDOT = regularNDOT,

            RestDayDuty = restResult,
            RestDayOT = restDayOT,
            RestDayND = restDayND,
            RestDayNDOT = restDayNDOT,

            LegalHoliday = legalHolResult,
            LegalHolOT = legalHolOT,
            LegalHolND = legalHolND,
            LegalHolNDOT = legalHolNDOT,

            SpecialWorkDay = specialWorkDayResult,
            SpecialNonWorkingOT = specialNonWorkingOT,
            SpecialNonWorkingND = specialNonWorkingND,
            SpecialNonWorkingNDOT = specialNonWorkingNDOT,

            RestSpecialDay = restSpecialDayResult,
            RestSpecialDayOT = restSpecialDayOT,
            RestSpecialDayND = restSpecialDayND,
            RestSpecialDayNDOT = restSpecialDayNDOT,

            RestLegalDay = restLegalDayResult,
            RestLegalDayND = restLegalDayND,
            RestLegalDayOT = restLegalDayOT,
            RestLegalDayNDOT = restLegalDayNDOT,

            DoubleLegal = doubleLegalResult,
            DoubleLegalOT = doubleLegalOT,
            DoubleLegalND = doubleLegalND,
            DoubleLegalNDOT = doubleLegalNDOT,

            RestDoubleLegal = restDoubleLegalResult,
            RestDoubleLegalOT = restDoubleLegalOT,
            RestDoubleLegalND = restDoubleLegalND,
            RestDoubleLegalNDOT = restDoubleLegalNDOT,

            LateHourInfo = lateResult.LateInfo,
            UTHourInfo = utResult.UTInfo,
            OTHourInfo = new OvertimeInfo
            {
                PayrollDate = context.PayrollDate,
                Hour = otHours,
                Amount = otTotal,
                Handler = "PerCategoryOT",
            },
            NightDiffInfo = new NightDiffInfo
            {
                PayrollDate = context.PayrollDate,
                Hour = ndAndNdotHours,
                Amount = ndTotal + ndotTotal,
                Handler = "PerCategoryND+NDOT",
            },
            //Leaves = leaveResult.Select(x => x.LeaveInfo).ToList(),
            TimeBaseGross = regularResult + leaveWithPay
                             + restResult
                             + otTotal
                             + ndTotal
                             + ndotTotal
                             + legalHolResult
                             + specialWorkDayResult
                             + restLegalDayResult
                             + restSpecialDayResult
                             + doubleLegalResult
                             + restDoubleLegalResult
        };
    }
}
