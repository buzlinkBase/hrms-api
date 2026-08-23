namespace Hrms.Core.Policies.DeductionPolicies
{
    public class TableWTaxDailyCalculator : IDeductionCalculator
    {
        private readonly ICutoffPolicyResolver _resolver;

        public TableWTaxDailyCalculator(ICutoffPolicyResolver resolver)
        {
            _resolver = resolver;
        }

        public DeductionPipeData Calculate(DeductionPayloadContext context, DeductionPipeData line)
        {
            if (line.IsLimit) return line;

            // 1. Compute Projected Rates
            // Daily projection often involves (Daily Gross * Working Days in Year)
            var calcResult = StatutoryHelper.GetDailyProjectedGrossRate(context);

            // Assuming table expects Annualized Gross
            decimal annualizedGross = calcResult.ProjectedGross * 12;

            // 2. Resolve Tax Table and Monthly Due
            var table = WTaxHelper.GetTable(context, annualizedGross);
            if (table == null) return line;

            var monthlyDue = WTaxHelper.GetCalculatedDue(table, annualizedGross);
            var balance = WTaxHelper.GetBalance(context, monthlyDue);

            if (balance <= 0) return line;

            // 3. Strategy Variables
            int divisor = calcResult.Divisor; // Total working days in month
            int? daysWorked = null;
            int? totalDaysInMonth = null;

            bool isLastDay = _resolver.IsLastCutoff(context);
            bool isCrossMonth = new IsCrossMonth().IsSatisfiedBy(context.Payload);

            // 4. Handle Logic for Settlement vs Daily Proration
            if (isLastDay || isCrossMonth)
            {
                // Last day of the month: Clear the remaining balance
                var finalPayload = new WTaxTablePayload(balance);
                line.Metadata["Status"] = "Month-End Settlement";
                return WTaxHelper.ApplyTable(context, line, finalPayload, context.Payload.FromDate);
            }

            if (StatutoryHelper.IsHiredThisMonth(context))
            {
                totalDaysInMonth = context.Payload.FromDate.GetDaysInMonth();
                var hireDate = StatutoryHelper.GetHiredDate(context);

                // Calculate days from hire until today for proration
                daysWorked = (int)hireDate.GetTotalDaysDiff(context.Payload.ToDate) + 1;
                divisor = 0; // Trigger proration path

                line.Metadata["Proration"] = $"{daysWorked}/{totalDaysInMonth}";
            }
            else
            {
                // Standard Daily: 
                // We divide the monthly due by total working days to get the daily tax
                divisor = calcResult.Divisor;
                line.Metadata["Divisor"] = divisor.ToString();
            }

            // 5. Calculate and Apply
            var payload = new WTaxTablePayload(
                StatutoryHelper.CalcRemainingBalance(monthlyDue, balance, divisor, daysWorked, totalDaysInMonth));

            return WTaxHelper.ApplyTable(context, line, payload, context.Payload.FromDate);
        }
    }
}