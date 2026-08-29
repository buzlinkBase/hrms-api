namespace Hrms.Core.Pipelines;

public class SpecialWorkDayPipeLine : BasicPipelineBase
{
    protected override void ConfigurePolicies(PayrollPipeLine<PayrollContext, BasicPipelineData> pipeline)
    {
        pipeline.AddPolicy(new SpecialWorkDayPolicy());
    }
}
