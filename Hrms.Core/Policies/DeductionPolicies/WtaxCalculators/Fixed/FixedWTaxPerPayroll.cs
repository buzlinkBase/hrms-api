namespace Hrms.Core.Policies.DeductionPolicies
{
    public class FixedWTaxPerPayroll : IDeductionCalculator
    {
        public DeductionPipeData Calculate(DeductionPayloadContext context, DeductionPipeData line)
        {
            var rate = context.Employee?.TaxRate;
            if (rate == null) return line;
            var payload = new WTaxTablePayload(rate.EE + rate.AddOns);
            return WTaxHelper.ApplyTable(context, line, payload, context.Payload.FromDate);
        }
    }

    public class FixedWTaxMonthlyCalculator : IDeductionCalculator
    {
        public DeductionPipeData Calculate(DeductionPayloadContext context, DeductionPipeData line)
        {
            var rate = context.Employee?.TaxRate;
            if (rate == null) return line;
            var balances = WTaxHelper.GetBalance(context, rate.EE);
            int divisor = 1;
            var payload = new WTaxTablePayload(StatutoryHelper.CalcRemainingBalance(rate.EE, balances, divisor));
            return WTaxHelper.ApplyTable(context, line, payload, context.Payload.FromDate);
        }
    }

    public class FixedWTaxSemiMonthlyCalculator : IDeductionCalculator
    {
        public DeductionPipeData Calculate(DeductionPayloadContext context, DeductionPipeData line)
        {
            var rate = context.Employee?.TaxRate;
            if (rate == null) return line;
            var balances = WTaxHelper.GetBalance(context, rate.EE);
            int divisor = 1;
            var resolver = new CutoffPolicyResolver();
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
                else if (currentCutoff.Day == firstCutoff.Day && balances == rate.EE)
                {
                    divisor = 2;
                }
                //else if (currentCutoff.Day == secondCutoff.Day)
                //{
                //    divisor = 1; // deduct remaining balance
                //}
            }
            catch (CutoffMismatchException)
            {
                //divisor = 1;
            }
            var payload = new WTaxTablePayload(
                StatutoryHelper.CalcRemainingBalance(rate.EE, balances, divisor));
            return WTaxHelper.ApplyTable(context, line, payload, context.Payload.FromDate);
        }
    }

    public class FixedWTaxWeeklyCalculator : IDeductionCalculator
    {
        public DeductionPipeData Calculate(DeductionPayloadContext context, DeductionPipeData line)
        {
            var rate = context.Employee?.TaxRate;
            if (rate == null) return line;

            var balances = WTaxHelper.GetBalance(context, rate.EE);
            int divisor = 1;
            // Cross-month or last week → deduct all remaining balance
            if (StatutoryHelper.IsCrossMonh(context) ||
                context.Payload.FromDate.IsLastWeekOfMonth() ||
                context.Payload.ToDate.IsLastWeekOfMonth())
            {
                divisor = 1;
            }
            else if (balances == rate.EE)
            {
                // Default: spread across all weeks in the month
                divisor = context.Payload.FromDate.GetNumberOfWeeksInMonth();
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
            var payload = new WTaxTablePayload(
                StatutoryHelper.CalcRemainingBalance(rate.EE, balances, divisor));

            return WTaxHelper.ApplyTable(context, line, payload, context.Payload.FromDate);
        }
    }
    public class FixedWTaxDailyCalculator : IDeductionCalculator
    {
        public DeductionPipeData Calculate(DeductionPayloadContext context, DeductionPipeData line)
        {
            var rate = context.Employee?.TaxRate;
            if (rate == null) return line;

            var balances = WTaxHelper.GetBalance(context, rate.EE);
            int daysInMonth = DateTime.DaysInMonth(context.Payload.FromDate.Year, context.Payload.FromDate.Month);

            if (new IsCrossMonth().IsSatisfiedBy(context.Payload))
            {
                var fullPayload = new WTaxTablePayload(
                    balances);

                return WTaxHelper.ApplyTable(context, line, fullPayload, context.Payload.FromDate);
            }

            int currentDay = context.Payload.FromDate.Day;
            int hireDay = context.Employee!.HireDate.Day;
            int effectiveDaysWorked = Math.Max(0, currentDay - hireDay + 1);

            var payload = new WTaxTablePayload(
                StatutoryHelper.CalcRemainingBalance(rate.EE, balances, daysInMonth) * effectiveDaysWorked);

            return WTaxHelper.ApplyTable(context, line, payload, context.Payload.FromDate);
        }
    }
}