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

            var payload = new SSSTablePayload(
                StatutoryHelper.CalcRemainingBalance(table.EE, balances.EEBalance, divisor),
                StatutoryHelper.CalcRemainingBalance(table.ER, balances.ERBalance, divisor),
                StatutoryHelper.CalcRemainingBalance(table.EC, balances.ECBalance, divisor));

            return SSSHelper.ApplyTable(context, line, payload, context.Payload.FromDate);
        }
    }
}