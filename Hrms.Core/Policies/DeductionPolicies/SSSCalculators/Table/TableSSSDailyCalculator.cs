namespace Hrms.Core.Policies.DeductionPolicies
{
    public class TableSSSDailyCalculator : IDeductionCalculator
    {
        public DeductionPipeData Calculate(DeductionPayloadContext context, DeductionPipeData line)
        {
            var resolver = new CutoffPolicyResolver();

            if (line.IsLimit) return line;

            // Compute projected daily gross rate
            var calcResult = StatutoryHelper.GetDailyProjectedGrossRate(context);
            decimal baseRate = calcResult.ProjectedGross;
            int divisor = calcResult.Divisor; // usually total working days in month

            var table = SSSHelper.GetTable(context, baseRate);
            if (table == null) return line;

            // Use helper to get balances
            var balances = SSSHelper.GetBalance(context, table.EE, table.ER, table.EC);

            if (balances.EEBalance == 0) return line;
            if (line.RemainingGrossBalance < balances.EEBalance) return line;

            var oneTimeFund = StatutoryHelper.IsOneTimePayLeave(context);
            if (oneTimeFund)
            {
                var payloadx = new SSSTablePayload(table.EE, table.ER, table.EC);
                return SSSHelper.ApplyTable(context, line, payloadx, context.Payload.FromDate);
            }

            // Current day of the payroll period
            int currentDay = context.Payload.FromDate.Day;

            // Effective day count = days worked so far
            int effectiveDayCount = currentDay;
            if (context.Employee.HireDate.Year == context.Payload.FromDate.Year &&
                context.Employee.HireDate.Month == context.Payload.FromDate.Month &&
                context.Employee.HireDate.Day > 1)
            {
                effectiveDayCount = Math.Max(0, currentDay - context.Employee.HireDate.Day + 1);
            }

            try
            {
                // If company defines daily cutoffs (e.g., weekly or custom), resolve them
                var currentCutoff = resolver.GetCurrentCutoff(context);

                // Adjust effective day count if cutoff mismatch
                if (currentDay < currentCutoff.Day)
                {
                    // fallback: clamp to cutoff day
                    effectiveDayCount = currentCutoff.Day;
                }
            }
            catch (CutoffMismatchException ex)
            {
                //AuditLogger.Warn(ex.Message);
                // fallback: keep effectiveDayCount as-is
            }

            var payload = new SSSTablePayload(
                StatutoryHelper.CalcRemainingBalance(table.EE, balances.EEBalance, divisor) * effectiveDayCount,
                StatutoryHelper.CalcRemainingBalance(table.ER, balances.ERBalance, divisor) * effectiveDayCount,
                StatutoryHelper.CalcRemainingBalance(table.EC, balances.ECBalance, divisor) * effectiveDayCount);

            return SSSHelper.ApplyTable(context, line, payload, context.Payload.FromDate);
        }
    }
}