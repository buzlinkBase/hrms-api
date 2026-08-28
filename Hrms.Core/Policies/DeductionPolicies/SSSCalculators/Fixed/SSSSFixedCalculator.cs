namespace Hrms.Core.Policies.DeductionPolicies
{
    public class FixedSSSPerPayroll : IDeductionCalculator
    {
        public DeductionPipeData Calculate(DeductionPayloadContext context, DeductionPipeData line)
        {
            var rate = context.Employee?.SSSRate;
            if (rate == null) return line;
            line.SSS.EE = rate.EE;
            line.SSS.ER = rate.ER + rate.EC;
            return line;
            //var payload = new SSSTablePayload(rate.EE + rate.AddOns, rate.ER, rate.EC);
            //return SSSHelper.ApplyTable(context, line, payload, context.Payload.FromDate);
        }
    }

    public class FixedSSSMonthlyCalculator : IDeductionCalculator
    {
        public DeductionPipeData Calculate(DeductionPayloadContext context, DeductionPipeData line)
        {
            var rate = context.Employee?.SSSRate;
            if (rate == null) return line;

            var balances = SSSHelper.GetBalance(context, rate.EE, rate.ER, rate.EC);

            int divisor = 1;
            // Use resolver instead of static 15th
            var resolver = new CutoffPolicyResolver();
            try
            {
                var firstCutoff = resolver.GetFirstCutoff(context);
                if (StatutoryHelper.IsHiredThisMonth(context) &&
                    StatutoryHelper.GetHiredDate(context).Day > firstCutoff.Day)
                {
                    divisor = 2; // half deduction if hired after cutoff
                }
            }
            catch (CutoffMismatchException)
            {
                divisor = 1;
            }

            var payload = new SSSTablePayload(
                StatutoryHelper.CalcRemainingBalance(rate.EE, balances.EEBalance, divisor),
                StatutoryHelper.CalcRemainingBalance(rate.ER, balances.ERBalance, divisor),
                StatutoryHelper.CalcRemainingBalance(rate.EC, balances.ECBalance, divisor));

            return SSSHelper.ApplyTable(context, line, payload, context.Payload.FromDate);
        }
    }

    public class FixedSSSSemiMonthlyCalculator : IDeductionCalculator
    {
        public DeductionPipeData Calculate(DeductionPayloadContext context, DeductionPipeData line)
        {
            var rate = context.Employee?.SSSRate;
            if (rate == null) return line;

            var balances = SSSHelper.GetBalance(context, rate.EE, rate.ER, rate.EC);

            int divisor = 1;
            var resolver = new CutoffPolicyResolver();

            var scheduleAction = StatutoryScheduleResolver.Resolve(context, resolver);
            if (scheduleAction == StatutoryReleaseAction.ReleaseNothing) return line;

            try
            {
                var firstCutoff = resolver.GetFirstCutoff(context);
                var secondCutoff = resolver.GetSecondCutoff(context);
                var currentCutoff = resolver.GetCurrentCutoff(context);

                if (StatutoryHelper.IsHiredThisMonth(context) &&
                    StatutoryHelper.GetHiredDate(context).Day > firstCutoff.Day)
                {
                    divisor = 1;
                }
                else if (currentCutoff.Day == firstCutoff.Day && balances.EEBalance == rate.EE)
                {
                    divisor = 2; // split into two deductions
                }
                else if (currentCutoff.Day == secondCutoff.Day)
                {
                    divisor = 1; // deduct remaining balance
                }
            }
            catch (CutoffMismatchException)
            {
                divisor = 1;
            }

            // FirstHalfMonth/SecondHalfMonth on their matching cutoff — release the full
            // remaining balance now instead of splitting it.
            if (scheduleAction == StatutoryReleaseAction.ReleaseFullBalanceNow) divisor = 1;

            var payload = new SSSTablePayload(
                StatutoryHelper.CalcRemainingBalance(rate.EE, balances.EEBalance, divisor),
                StatutoryHelper.CalcRemainingBalance(rate.ER, balances.ERBalance, divisor),
                StatutoryHelper.CalcRemainingBalance(rate.EC, balances.ECBalance, divisor));

            return SSSHelper.ApplyTable(context, line, payload, context.Payload.FromDate);
        }
    }

    public class FixedSSSWeeklyCalculator : IDeductionCalculator
    {
        public DeductionPipeData Calculate(DeductionPayloadContext context, DeductionPipeData line)
        {
            var rate = context.Employee?.SSSRate;
            if (rate == null) return line;

            var balances = SSSHelper.GetBalance(context, rate.EE, rate.ER, rate.EC);

            var resolver = new CutoffPolicyResolver();
            var scheduleAction = StatutoryScheduleResolver.Resolve(context, resolver);
            if (scheduleAction == StatutoryReleaseAction.ReleaseNothing) return line;

            int divisor = 1;
            // Cross-month or last week → deduct all remaining balance
            if (StatutoryHelper.IsCrossMonh(context) ||
                context.Payload.FromDate.IsLastWeekOfMonth() ||
                context.Payload.ToDate.IsLastWeekOfMonth())
            {
                divisor = 1;
            }
            else
            {
                // Recompute the remaining-weeks-in-month divisor every time (not just "if
                // nothing withheld yet") so it decreases correctly week over week once prior
                // withholding is actually persisted, instead of sweeping the entire
                // remaining balance the first time the ledger isn't empty.
                divisor = context.Payload.FromDate.GetRemainingWeeksInMonth();
            }

            //  Mid-month hire logic: only remaining weeks count
            if (StatutoryHelper.IsHiredThisMonth(context))
            {
                if (StatutoryHelper.IsCrossMonh(context))
                {
                    // Cross-month: compute remaining weeks between FromDate and ToDate
                    var from = context.Payload.FromDate.ToDateTime(TimeOnly.MinValue);
                    var to = context.Payload.ToDate.ToDateTime(TimeOnly.MinValue);

                    int daysRemaining = (to - from).Days + 1;
                    divisor = (int)Math.Ceiling(daysRemaining / 7.0);
                }
                else
                {
                    // Same-month: use extension method
                    divisor = context.Payload.FromDate.GetRemainingWeeksInMonth();
                }
            }

            var payload = new SSSTablePayload(
                StatutoryHelper.CalcRemainingBalance(rate.EE, balances.EEBalance, divisor),
                StatutoryHelper.CalcRemainingBalance(rate.ER, balances.ERBalance, divisor),
                StatutoryHelper.CalcRemainingBalance(rate.EC, balances.ECBalance, divisor));

            return SSSHelper.ApplyTable(context, line, payload, context.Payload.FromDate);
        }
    }
    public class FixedSSSDailyCalculator : IDeductionCalculator
    {
        public DeductionPipeData Calculate(DeductionPayloadContext context, DeductionPipeData line)
        {
            var rate = context.Employee?.SSSRate;
            if (rate == null) return line;

            var balances = SSSHelper.GetBalance(context, rate.EE, rate.ER, rate.EC);
            int daysInMonth = DateTime.DaysInMonth(context.Payload.FromDate.Year, context.Payload.FromDate.Month);

            if (new IsCrossMonth().IsSatisfiedBy(context.Payload))
            {
                var fullPayload = new SSSTablePayload(
                    balances.EEBalance,
                    balances.ERBalance,
                    balances.ECBalance);

                return SSSHelper.ApplyTable(context, line, fullPayload, context.Payload.FromDate);
            }

            int currentDay = context.Payload.FromDate.Day;
            int hireDay = context.Employee.HireDate.Day;
            int effectiveDaysWorked = Math.Max(0, currentDay - hireDay + 1);

            var payload = new SSSTablePayload(
                StatutoryHelper.CalcRemainingBalance(rate.EE, balances.EEBalance, daysInMonth) * effectiveDaysWorked,
                StatutoryHelper.CalcRemainingBalance(rate.ER, balances.ERBalance, daysInMonth) * effectiveDaysWorked,
                StatutoryHelper.CalcRemainingBalance(rate.EC, balances.ECBalance, daysInMonth) * effectiveDaysWorked);
            return SSSHelper.ApplyTable(context, line, payload, context.Payload.FromDate);

        }
    }

}