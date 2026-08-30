using Hrms.Core;
using Hrms.Core.Policies.DeductionPolicies;

public class TablePHICSemiMonthlyCalculator : IDeductionCalculator
{
    private readonly CutoffDivisorResolver _divisorResolver = new();

    public DeductionPipeData Calculate(DeductionPayloadContext context, DeductionPipeData line)
    {
        if (line.IsLimit) return line;
        var resolver = new CutoffPolicyResolver();
        var baseRate = StatutoryHelper.GetSemiMonthlyGrossBaseRate(context);
        var table = PHICHelper.GetTable(context, baseRate);
        if (table == null) return line;

        var balances = PHICHelper.GetBalance(context, table.EmployeeShare, table.EmployerShare);
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
                var payload = new PHICTablePayload(
                    strategy.AllocateFirstCutoffShare(table.EmployeeShare, balances.EEBalance, context, divisor),
                    strategy.AllocateFirstCutoffShare(table.EmployerShare, balances.ERBalance, context, divisor));

                return PHICHelper.ApplyTable(context, line, payload, date);
            }
            else if (resolver.IsSecondCutoff(context))
            {
                // Always the exact remaining balance — self-corrects the month's total to
                // table.EmployeeShare/EmployerShare regardless of how the first cutoff split
                // it, so Fixed and Variable employees both end up contributing the correct
                // full-month amount.
                var payload = new PHICTablePayload(
                    balances.EEBalance,
                    balances.ERBalance);

                return PHICHelper.ApplyTable(context, line, payload, date);
            }
        }
        catch (CutoffMismatchException)
        {
            var payload = new PHICTablePayload(
                balances.EEBalance,
                balances.ERBalance);

            return PHICHelper.ApplyTable(context, line, payload, date);
        }

        return line;
    }
}