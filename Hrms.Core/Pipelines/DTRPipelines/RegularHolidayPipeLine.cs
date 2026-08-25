namespace Hrms.Core.Pipelines;

public class RegularHolidayPipeLine : BasicPipelineBase
{
    protected override void ConfigurePolicies(PayrollPipeLine<PayrollContext, BasicPipelineData> pipeline)
    {
        pipeline.AddPolicy(new RegularHolidayPolicy());
    }
}
