using Hrms.Core;
using Hrms.Core.Policies.DeductionPolicies;

public class TableSSSSemiMonthlyCalculator : IDeductionCalculator
{
    private readonly CutoffDivisorResolver _divisorResolver = new();

    public DeductionPipeData Calculate(DeductionPayloadContext context, DeductionPipeData line)
    {
        if (line.IsLimit) return line;
        var resolver = new CutoffPolicyResolver();
        var baseRate =  StatutoryHelper.GetSemiMonthlyGrossBaseRate(context);
        var table = SSSHelper.GetTable(context, baseRate);
        if (table == null) return line;

        var balances = SSSHelper.GetBalance(context, table.EE, table.ER, table.EC);
        if (balances.EEBalance == 0) return line;
        if (line.RemainingGrossBalance < balances.EEBalance) return line;

        var scheduleAction = StatutoryScheduleResolver.Resolve(context, resolver);
        if (scheduleAction == StatutoryReleaseAction.ReleaseNothing) return line;

        var divisor = _divisorResolver.Resolve(context, resolver, scheduleAction);
        var date = context.Payload.FromDate;

        try
        {
            if (resolver.IsFirstCutoff(context))
            {
                var strategy = CutoffAllocationStrategyFactory.Resolve(context.Employee.SalaryType);
                var payload = new SSSTablePayload(
                    strategy.AllocateFirstCutoffShare(table.EE, balances.EEBalance, context, divisor),
                    strategy.AllocateFirstCutoffShare(table.ER, balances.ERBalance, context, divisor),
                    strategy.AllocateFirstCutoffShare(table.EC, balances.ECBalance, context, divisor));

                return SSSHelper.ApplyTable(context, line, payload, date);
            }
            else if (resolver.IsSecondCutoff(context))
            {
                // Always the exact remaining balance — self-corrects the month's total to
                // table.EE/ER/EC regardless of how the first cutoff split it, so Fixed and
                // Variable employees both end up contributing the correct full-month amount.
                var payload = new SSSTablePayload(
                    balances.EEBalance,
                    balances.ERBalance,
                    balances.ECBalance);
                return SSSHelper.ApplyTable(context, line, payload, date);
            }
        }
        catch (CutoffMismatchException)
        {
            var payload = new SSSTablePayload(
                balances.EEBalance,
                balances.ERBalance,
                balances.ECBalance);
            return SSSHelper.ApplyTable(context, line, payload, date);
        }
        return line;
    }
}
