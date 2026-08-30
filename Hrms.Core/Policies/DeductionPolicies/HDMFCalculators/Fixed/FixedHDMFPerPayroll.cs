namespace Hrms.Core.Policies.DeductionPolicies
{
    public class FixedHDMFPerPayroll : IDeductionCalculator
    {
        public DeductionPipeData Calculate(DeductionPayloadContext context, DeductionPipeData line)
        {
            var rate = context.Employee?.HDMFRate;
            if (rate == null) return line;

            var payload = new HDMFTablePayload(rate.EE + rate.AddOns, rate.ER);
            return HDMFHelper.ApplyTable(context, line, payload, context.Payload.FromDate);
        }
    }
}