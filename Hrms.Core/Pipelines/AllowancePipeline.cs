using Hrms.Core.Policies.OtherIncome;

namespace Hrms.Core.Pipelines;

public class AllowancePipeline : BasePipelineBase<PayrollContext, AllowancePipeData>
{
    public override AllowancePipeData Run(PayrollContext context)
    {
        pipeline
            .AddPolicy(new ScheduledIncomePolicy())
            .AddPolicy(new ColaPolicy());
        return pipeline.Execute(new AllowancePipeData(), context);
    }
}

