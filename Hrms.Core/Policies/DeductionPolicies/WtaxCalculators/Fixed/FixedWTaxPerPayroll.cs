namespace Hrms.Core.Policies.DeductionPolicies
{
    public class FixedWTaxPerPayroll : IDeductionCalculator
    {
        public DeductionPipeData Calculate(DeductionPayloadContext context, DeductionPipeData line)
        {
            var rate = context.Employee?.TaxRate;
            if (rate == null) return line;
            var payload = new WTaxTablePayload(rate.EE + rate.AddOns);
            return WTaxHelper.ApplyTable(context, line, payload, context.Payload.FromDate);
        }
    }
}