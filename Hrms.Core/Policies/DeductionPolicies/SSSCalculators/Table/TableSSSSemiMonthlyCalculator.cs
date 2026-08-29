using Hrms.Core;
using Hrms.Core.Policies.DeductionPolicies;

public class TableSSSSemiMonthlyCalculator : IDeductionCalculator
{
    public DeductionPipeData Calculate(DeductionPayloadContext context, DeductionPipeData line)
    {
        var resolver = new CutoffPolicyResolver();

        if (line.IsLimit) return line;

        var baseRate = StatutoryHelper.GetSemiMonthlyGrossBaseRate(context);
        var table = SSSHelper.GetTable(context, baseRate);
        if (table == null) return line;

        // Use helper to get balances
        var balances = SSSHelper.GetBalance(context, table.EE, table.ER, table.EC);

        if (balances.EEBalance == 0) return line;
        if (line.RemainingGrossBalance < balances.EEBalance) return line;

        var scheduleAction = StatutoryScheduleResolver.Resolve(context, resolver);
        if (scheduleAction == StatutoryReleaseAction.ReleaseNothing) return line;

        int divisor = 2; // default semi-monthly split

        // Cross-month payroll → deduct all remaining immediately
        if (new IsCrossMonth().IsSatisfiedBy(context.Payload))
        {
            divisor = 1;
        }
        // Mid-period hire → half deduction if joined after first cutoff
        else if (context.Employee.HireDate.Year == context.Payload.FromDate.Year &&
                 context.Employee.HireDate.Month == context.Payload.FromDate.Month)
        {
            var currentCutoff = resolver.GetCurrentCutoff(context);
            if (context.Employee.HireDate.Day > currentCutoff.Day)
            {
                divisor = 2;
            }
        }

        // FirstHalfMonth/SecondHalfMonth on their matching cutoff — release the full
        // remaining balance now instead of splitting it.
        if (scheduleAction == StatutoryReleaseAction.ReleaseFullBalanceNow) divisor = 1;

        var date = context.Payload.FromDate;

        try
        {
            if (resolver.IsFirstCutoff(context))
            {
                // First cutoff → half deduction (or prorated if late hire)
                var payload = new SSSTablePayload(
                    StatutoryHelper.CalcRemainingBalance(table.EE, balances.EEBalance, divisor),
                    StatutoryHelper.CalcRemainingBalance(table.ER, balances.ERBalance, divisor),
                    StatutoryHelper.CalcRemainingBalance(table.EC, balances.ECBalance, divisor));

                return SSSHelper.ApplyTable(context, line, payload, date);
            }
            else if (resolver.IsSecondCutoff(context))
            {
                // Second cutoff → deduct remaining balances
                var payload = new SSSTablePayload(
                    balances.EEBalance,
                    balances.ERBalance,
                    balances.ECBalance);

                return SSSHelper.ApplyTable(context, line, payload, date);
            }
        }
        catch (CutoffMismatchException ex)
        {
            // Audit log: mismatch detected
            //AuditLogger.Warn(ex.Message);

            // Fallback: deduct at end of period
            var payload = new SSSTablePayload(
                balances.EEBalance,
                balances.ERBalance,
                balances.ECBalance);

            return SSSHelper.ApplyTable(context, line, payload, date);
        }

        return line;
    }
}