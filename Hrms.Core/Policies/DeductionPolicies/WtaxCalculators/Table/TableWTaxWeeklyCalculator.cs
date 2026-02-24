namespace Hrms.Core.Policies.DeductionPolicies
{
    public class TableWTaxWeeklyCalculator : IDeductionCalculator
    {
        private readonly ICutoffPolicyResolver _resolver;

        public TableWTaxWeeklyCalculator(ICutoffPolicyResolver resolver)
        {
            _resolver = resolver;
        }

        public DeductionPipeData Calculate(DeductionPayloadContext context, DeductionPipeData line)
        {
            if (line.IsLimit) return line;

            // 1. Compute Taxable Income (Gross - Statutory EE shares)
            var baseRate = StatutoryHelper.GetWeeklyGrossBaseRate(context)
                           - (line.SSS?.EE ?? 0)
                           - (line.PHIC?.EE ?? 0)
                           - (line.HDMF?.EE ?? 0);

            // 2. Resolve Tax Table and Due Amount
            var table = WTaxHelper.GetTable(context, baseRate);
            if (table == null) return line;

            var due = WTaxHelper.GetCalculatedDue(table);
            var balance = WTaxHelper.GetBalance(context, due);

            if (balance <= 0) return line;
            if (line.RemainingGrossBalance < balance) return line;

            // 3. Deduction Strategy Variables
            int divisor = 1;
            int? daysWorked = null;
            int? totalDaysInMonth = null;

            bool isLastWeek = _resolver.IsLastCutoff(context);
            bool isCrossMonth = new IsCrossMonth().IsSatisfiedBy(context.Payload);

            // 4. Resolve Divisor or Proration
            if (isLastWeek || isCrossMonth)
            {
                // Final run of the month: Take everything left
                divisor = 1;
            }
            else if (StatutoryHelper.IsHiredThisMonth(context))
            {
                // Mid-Month Hire Path
                totalDaysInMonth = context.Payload.FromDate.GetDaysInMonth();
                var hireDate = StatutoryHelper.GetHiredDate(context);

                // Calculate days from hire until the end of this payroll period
                daysWorked = (int)hireDate.GetTotalDaysDiff(context.Payload.ToDate) + 1;

                // If not in the last week, spread by remaining weeks
                // Otherwise, divisor remains 0 to trigger the StatutoryHelper proration logic
                if (!isLastWeek)
                {
                    divisor = context.Payload.FromDate.GetRemainingWeeksInMonth();
                }
                else
                {
                    divisor = 0; // Proration path in CalcRemainingBalance
                }
            }
            else if (due == balance)
            {
                // First week of the month for existing employees: Spread by total weeks (4 or 5)
                divisor = context.Payload.FromDate.GetNumberOfWeeksInMonth();
            }
            else
            {
                // Intermediate weeks: Spread by remaining weeks to stay balanced
                divisor = context.Payload.FromDate.GetRemainingWeeksInMonth();
            }

            // 5. Metadata for Audit clarity
            if (daysWorked.HasValue)
                line.Metadata["Proration"] = $"{daysWorked}/{totalDaysInMonth}";
            else
                line.Metadata["Divisor"] = divisor.ToString();

            // 6. Calculate and Apply
            var payload = new WTaxTablePayload(
                StatutoryHelper.CalcRemainingBalance(due, balance, divisor, daysWorked, totalDaysInMonth));

            return WTaxHelper.ApplyTable(context, line, payload, context.Payload.FromDate);
        }
    }
}