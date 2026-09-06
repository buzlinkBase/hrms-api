using Hrms.Core;
using Hrms.Core.Policies.DeductionPolicies;

public class TablePHICSemiMonthlyCalculator : IDeductionCalculator
{
    private readonly CutoffDivisorResolver _divisorResolver = new();

    public DeductionPipeData Calculate(DeductionPayloadContext context, DeductionPipeData line)
    {
        if (line.IsLimit) return line;
        var resolver = new CutoffPolicyResolver();
        var baseRate = StatutoryHelper.GetSemiMonthlyBracketBaseRate(context, resolver);
        var table = PHICHelper.GetTable(context, baseRate);
        if (table == null) return line;

        var balances = PHICHelper.GetBalance(context, table.EmployeeShare, table.EmployerShare);
        if (balances.EEBalance == 0) return line;
        if (line.RemainingGrossBalance < balances.EEBalance) return line;

        var date = context.Payload.FromDate;
        var oneTimeFund = StatutoryHelper.IsOneTimePayLeave(context);
        if (oneTimeFund)
        {
            var payload = new PHICTablePayload(table.EmployeeShare, table.EmployerShare);
            return PHICHelper.ApplyTable(context, line, payload, date);
        }

        try
        {
            if (resolver.IsFirstCutoff(context))
            {
                var scheduleAction = StatutoryScheduleResolver.Resolve(context, resolver);
                if (scheduleAction == StatutoryReleaseAction.ReleaseNothing) return line; // e.g. SecondHalfMonth: wait for the last cutoff

                var divisor = _divisorResolver.Resolve(context, resolver, scheduleAction);
                var strategy = CutoffAllocationStrategyFactory.Resolve(context.Employee.SalaryType);
                var payload = new PHICTablePayload(
                    strategy.AllocateFirstCutoffShare(table.EmployeeShare, balances.EEBalance, context, divisor),
                    strategy.AllocateFirstCutoffShare(table.EmployerShare, balances.ERBalance, context, divisor));

                return PHICHelper.ApplyTable(context, line, payload, date);
            }
            else if (resolver.IsSecondCutoff(context))
            {
                // Always the exact remaining balance, regardless of StatutoryDeductionSchedule
                // — the last cutoff is the final chance in the month to true up whatever the
                // first cutoff did/didn't withhold, so Fixed and Variable employees (and every
                // schedule: PerPayroll, FirstHalfMonth, SecondHalfMonth) all converge on the
                // correct full-month total no matter how the first cutoff split it. Must stay
                // unconditional — this used to be gated behind a StatutoryScheduleResolver
                // check that made it unreachable under FirstHalfMonth (see git history).
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