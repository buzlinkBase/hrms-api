namespace Hrms.Core.Policies.DeductionPolicies
{
    public class TableWTaxSemiMonthlyCalculator : IDeductionCalculator
    {
        private readonly ICutoffPolicyResolver _resolver;

        public TableWTaxSemiMonthlyCalculator(ICutoffPolicyResolver resolver)
        {
            _resolver = resolver;
        }

        public DeductionPipeData Calculate(DeductionPayloadContext context, DeductionPipeData line)
        {
            if (line.IsLimit) return line;

            // 1. Compute Base Taxable Income
            // (Semi-Monthly Gross minus the Employee shares of SSS, PhilHealth, and Pag-IBIG)
            var baseRate = StatutoryHelper.GetSemiMonthlyGrossBaseRate(context)
                         - (line.SSS?.EE ?? 0)
                         - (line.PHIC?.EE ?? 0)
                         - (line.HDMF?.EE ?? 0);

            // 2. Lookup the Tax Table and Calculate Total Monthly Due
            var table = WTaxHelper.GetTable(context, baseRate);
            if (table == null) return line;

            var due = WTaxHelper.GetCalculatedDue(table);
            var balance = WTaxHelper.GetBalance(context, due);

            // If already fully paid for the month, exit
            if (balance <= 0) return line;

            // 3. Deduction Strategy Variables
            int divisor = 2; // Default split for semi-monthly (1st and 2nd cutoff)
            int? daysWorked = null;
            int? totalDaysInMonth = null;

            bool isFirstCutoff = _resolver.IsFirstCutoff(context);
            bool isLastCutoff = _resolver.IsLastCutoff(context);
            bool isCrossMonth = new IsCrossMonth().IsSatisfiedBy(context.Payload);

            // 4. Resolve the Strategy (Split vs. Full Settlement)
            if (isLastCutoff || isCrossMonth)
            {
                // We are in the 2nd cutoff: Deduct 100% of the remaining balance
                divisor = 1;
                line.Metadata["Status"] = "Month-End Settlement";
            }
            else if (StatutoryHelper.IsHiredThisMonth(context))
            {
                // Mid-Month Hire Logic
                totalDaysInMonth = context.Payload.FromDate.GetDaysInMonth();
                var hireDate = StatutoryHelper.GetHiredDate(context);
                daysWorked = (int)hireDate.GetTotalDaysDiff(context.Payload.ToDate) + 1;

                var firstCutoff = _resolver.GetFirstCutoff(context);

                // If they were hired AFTER the first cutoff date, they only have one run left.
                // We use divisor 0 to trigger the Proration Path in StatutoryHelper.
                if (hireDate.Day > firstCutoff.Day)
                {
                    divisor = 0;
                    line.Metadata["Proration"] = $"{daysWorked}/{totalDaysInMonth}";
                }
                else
                {
                    divisor = 2; // Hired early enough to split across both cutoffs
                }
            }
            else if (isFirstCutoff)
            {
                // Standard 1st Cutoff: Split the total monthly tax by 2
                divisor = 2;
                line.Metadata["Divisor"] = "2";
            }

            // 5. Calculate final payload
            var payload = new WTaxTablePayload(
                StatutoryHelper.CalcRemainingBalance(due, balance, divisor, daysWorked, totalDaysInMonth));

            // 6. Apply to the pipe
            return WTaxHelper.ApplyTable(context, line, payload, context.Payload.FromDate);
        }
    }
}