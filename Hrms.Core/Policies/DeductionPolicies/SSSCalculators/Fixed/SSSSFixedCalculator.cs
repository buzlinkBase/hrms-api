namespace Hrms.Core.Policies.DeductionPolicies
{
    public class FixedSSSPerPayroll : IDeductionCalculator
    {
        public DeductionPipeData Calculate(DeductionPayloadContext context, DeductionPipeData line)
        {
            if (line.IsLimit) return line;
            var rate = context.Employee?.SSSRate;
            if (rate == null) return line;

            var payload = new SSSTablePayload(rate.EE, rate.ER, rate.EC);
            return SSSHelper.ApplyTable(context, line, payload, context.Payload.FromDate);
        }
    }
}
