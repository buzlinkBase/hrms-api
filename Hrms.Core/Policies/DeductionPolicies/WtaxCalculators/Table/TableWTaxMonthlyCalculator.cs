namespace Hrms.Core.Policies.DeductionPolicies
{
    public class TableWTaxMonthlyCalculator : IDeductionCalculator
    {
        private readonly ICutoffPolicyResolver _resolver;

        public TableWTaxMonthlyCalculator(ICutoffPolicyResolver resolver)
        {
            _resolver = resolver;
        }

        public DeductionPipeData Calculate(DeductionPayloadContext context, DeductionPipeData line)
        {
            // 1. Resolve Monthly Taxable Income
            var baseRate = StatutoryHelper.GetMonthlyGrossBaseRate(context)
                           - (line.SSS?.EE ?? 0)
                           - (line.PHIC?.EE ?? 0)
                           - (line.HDMF?.EE ?? 0);

            // 2. Resolve Tax Table
            var table = WTaxHelper.GetTable(context, baseRate);
            if (table == null) return line;

            var due = WTaxHelper.GetCalculatedDue(table);
            var balance = WTaxHelper.GetBalance(context, due);

            if (balance <= 0) return line;

            // 3. Deduction Strategy Variables
            int divisor = 1; // Default for Monthly
            int? daysWorked = null;
            int? totalDaysInMonth = null;

            bool isCrossMonth = new IsCrossMonth().IsSatisfiedBy(context.Payload);

            // 4. Handle Mid-Month Hire Proration
            if (StatutoryHelper.IsHiredThisMonth(context))
            {
                totalDaysInMonth = context.Payload.FromDate.GetDaysInMonth();
                var hireDate = StatutoryHelper.GetHiredDate(context);

                // Days from hire until the end of the payroll period
                daysWorked = (int)hireDate.GetTotalDaysDiff(context.Payload.ToDate) + 1;

                // Even in Monthly, if the hire date is after the configured main cutoff,
                // we might treat it as a proration event.
                var currentCutoff = _resolver.GetCurrentCutoff(context);
                if (hireDate.Day > currentCutoff.Day || isCrossMonth)
                {
                    divisor = 0; // Triggers proration path in CalcRemainingBalance
                }
            }

            // 5. Metadata for Audit
            if (daysWorked.HasValue)
                line.Metadata["Proration"] = $"{daysWorked}/{totalDaysInMonth}";
            else
                line.Metadata["Divisor"] = divisor.ToString();

            // 6. Apply Tax
            var payload = new WTaxTablePayload(
                StatutoryHelper.CalcRemainingBalance(due, balance, divisor, daysWorked, totalDaysInMonth));

            return WTaxHelper.ApplyTable(context, line, payload, context.Payload.FromDate);
        }
    }
}