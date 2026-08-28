namespace Hrms.Core.Policies.DeductionPolicies
{
    public class FixedHDMFPerPayroll : IDeductionCalculator
    {
        public DeductionPipeData Calculate(DeductionPayloadContext context, DeductionPipeData line)
        {
            var rate = context.Employee?.HDMFRate;
            if (rate == null) return line;

            var payload = new HDMFTablePayload(rate.EE + rate.AddOns, rate.ER);
            return HDMFHelper.ApplyTable(context, line, payload, context.Payload.FromDate);
        }
    }

    public class FixedHDMFMonthlyCalculator : IDeductionCalculator
    {
        public DeductionPipeData Calculate(DeductionPayloadContext context, DeductionPipeData line)
        {
            var rate = context.Employee?.HDMFRate;
            if (rate == null) return line;

            var balances = HDMFHelper.GetBalance(context, rate.EE, rate.ER);

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

            var payload = new HDMFTablePayload(
                StatutoryHelper.CalcRemainingBalance(rate.EE, balances.EEBalance, divisor),
                StatutoryHelper.CalcRemainingBalance(rate.ER, balances.ERBalance, divisor));

            return HDMFHelper.ApplyTable(context, line, payload, context.Payload.FromDate);
        }
    }

    public class FixedHDMFSemiMonthlyCalculator : IDeductionCalculator
    {
        public DeductionPipeData Calculate(DeductionPayloadContext context, DeductionPipeData line)
        {
            var rate = context.Employee?.HDMFRate;
            if (rate == null) return line;

            var balances = HDMFHelper.GetBalance(context, rate.EE, rate.ER);

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
                    divisor = 2;
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

            var payload = new HDMFTablePayload(
                StatutoryHelper.CalcRemainingBalance(rate.EE, balances.EEBalance, divisor),
                StatutoryHelper.CalcRemainingBalance(rate.ER, balances.ERBalance, divisor));

            return HDMFHelper.ApplyTable(context, line, payload, context.Payload.FromDate);
        }
    }

    public class FixedHDMFWeeklyCalculator : IDeductionCalculator
    {
        public DeductionPipeData Calculate(DeductionPayloadContext context, DeductionPipeData line)
        {
            var rate = context.Employee?.HDMFRate;
            if (rate == null) return line;

            var balances = HDMFHelper.GetBalance(context, rate.EE, rate.ER);

            var resolver = new CutoffPolicyResolver();
            var scheduleAction = StatutoryScheduleResolver.Resolve(context, resolver);
            if (scheduleAction == StatutoryReleaseAction.ReleaseNothing) return line;

            int divisor = 1;

            // Cross-month or last week → deduct all remaining balance
            if (new IsCrossMonth().IsSatisfiedBy(context.Payload) ||
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
                if (new IsCrossMonth().IsSatisfiedBy(context.Payload))
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

            // FirstHalfMonth/SecondHalfMonth on their matching cutoff — release the full
            // remaining balance now instead of splitting it.
            if (scheduleAction == StatutoryReleaseAction.ReleaseFullBalanceNow) divisor = 1;

            var payload = new HDMFTablePayload(
                StatutoryHelper.CalcRemainingBalance(rate.EE, balances.EEBalance, divisor),
                StatutoryHelper.CalcRemainingBalance(rate.ER, balances.ERBalance, divisor));

            return HDMFHelper.ApplyTable(context, line, payload, context.Payload.FromDate);
        }
    }
    public class FixedHDMFDailyCalculator : IDeductionCalculator
    {
        public DeductionPipeData Calculate(DeductionPayloadContext context, DeductionPipeData line)
        {
            var rate = context.Employee?.HDMFRate;
            if (rate == null) return line;

            var balances = HDMFHelper.GetBalance(context, rate.EE, rate.ER);
            int daysInMonth = DateTime.DaysInMonth(context.Payload.FromDate.Year, context.Payload.FromDate.Month);

            if (new IsCrossMonth().IsSatisfiedBy(context.Payload))
            {
                var fullPayload = new HDMFTablePayload(
                    balances.EEBalance,
                    balances.ERBalance);

                return HDMFHelper.ApplyTable(context, line, fullPayload, context.Payload.FromDate);
            }

            int currentDay = context.Payload.FromDate.Day;
            int hireDay = context.Employee.HireDate.Day;
            int effectiveDaysWorked = Math.Max(0, currentDay - hireDay + 1);

            var payload = new HDMFTablePayload(
                StatutoryHelper.CalcRemainingBalance(rate.EE, balances.EEBalance, daysInMonth) * effectiveDaysWorked,
                StatutoryHelper.CalcRemainingBalance(rate.ER, balances.ERBalance, daysInMonth) * effectiveDaysWorked);

            return HDMFHelper.ApplyTable(context, line, payload, context.Payload.FromDate);
        }
    }
}