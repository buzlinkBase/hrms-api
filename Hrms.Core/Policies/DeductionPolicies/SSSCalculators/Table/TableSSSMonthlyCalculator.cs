namespace Hrms.Core.Policies.DeductionPolicies
{
    public class TableSSSMonthlyCalculator : IDeductionCalculator
    {
        public DeductionPipeData Calculate(DeductionPayloadContext context, DeductionPipeData line)
        {
            var resolver = new CutoffPolicyResolver();
            var getGrossIncome = StatutoryHelper.GetMonthlyGrossBaseRate(context);
            var table = SSSHelper.GetTable(context, getGrossIncome);
            if (table == null) return line;
            var balances = SSSHelper.GetBalance(context, table.EE, table.ER, table.EC);
            var divisor = 1;

            var strategy = CutoffAllocationStrategyFactory.Resolve(context.Employee.SalaryType);
            var payload = new SSSTablePayload(
                strategy.AllocateFirstCutoffShare(table.EE, balances.EEBalance, context, divisor),
                strategy.AllocateFirstCutoffShare(table.ER, balances.ERBalance, context, divisor),
                strategy.AllocateFirstCutoffShare(table.EC, balances.ECBalance, context, divisor));

            return SSSHelper.ApplyTable(context, line, payload, context.Payload.FromDate);
        }
    }
}