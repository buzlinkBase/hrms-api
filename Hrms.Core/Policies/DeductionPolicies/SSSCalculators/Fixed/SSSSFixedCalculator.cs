namespace Hrms.Core.Policies.DeductionPolicies
{
    public class FixedSSSPerPayroll : IDeductionCalculator
    {
        public DeductionPipeData Calculate(DeductionPayloadContext context, DeductionPipeData line)
        {
            var rate = context.Employee?.SSSRate;
            if (rate == null) return line;
            line.SSS.EE = rate.EE;
            line.SSS.ER = rate.ER + rate.EC;
            return line;
        }
    }
}