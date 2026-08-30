namespace Hrms.Core.Policies.DeductionPolicies
{
    public class FixedPHICPerPayroll : IDeductionCalculator
    {
        public DeductionPipeData Calculate(DeductionPayloadContext context, DeductionPipeData line)
        {
            var rate = context.Employee?.PHICRate;
            if (rate == null) return line;

            var payload = new PHICTablePayload(rate.EE + rate.AddOns, rate.ER);
            return PHICHelper.ApplyTable(context, line, payload, context.Payload.FromDate);
        }
    }
}