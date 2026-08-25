
using Hrms.Core.Policies.DeductionPolicies;

namespace Hrms.Core.Pipelines;

public class DeductionPipeline : BasePipelineBase<DeductionPayloadContext, DeductionPipeData>
{
    protected override void ConfigurePolicies(PayrollPipeLine<DeductionPayloadContext, DeductionPipeData> pipeline)
    {
        pipeline
            .AddPolicy(new SSSPolicy())
            .AddPolicy(new PHICPolicy())
            .AddPolicy(new HDMFPolicy())
            .AddPolicy(new WTaxPolicy())
            .AddPolicy(new ScheduledDeductionPolicy())
            ;
    }

    protected override DeductionPipeData CreateSeed(DeductionPayloadContext context) =>
        new DeductionPipeData { RemainingGrossBalance = context.PayrollLine.GrossIncome };
}