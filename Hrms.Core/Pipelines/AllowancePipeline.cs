using Hrms.Core.Policies.OtherIncome;

namespace Hrms.Core.Pipelines;

public class AllowancePipeline : BasePipelineBase<PayrollContext, AllowancePipeData>
{
    protected override void ConfigurePolicies(PayrollPipeLine<PayrollContext, AllowancePipeData> pipeline)
    {
        pipeline
            .AddPolicy(new ScheduledIncomePolicy())
            .AddPolicy(new ColaPolicy());
    }
}

