
using Hrms.Core.Policies.DeductionPolicies;

namespace Hrms.Core.Pipelines;

public class DeductionPipeline : BasePipelineBase<DeductionPayloadContext, DeductionPipeData>
{
    public override DeductionPipeData Run(DeductionPayloadContext context)
    {

        pipeline
            .AddPolicy(new SSSPolicy())
            .AddPolicy(new PHICPolicy())
            .AddPolicy(new HDMFPolicy())
            .AddPolicy(new WTaxPolicy())
            .AddPolicy(new ScheduledDeductionPolicy())
            ;

        return pipeline.Execute(new DeductionPipeData() { RemainingGrossBalance = context.PayrollLine.GrossIncome }, context);
    }
}